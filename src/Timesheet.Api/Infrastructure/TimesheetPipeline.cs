using MongoDB.Bson;
using Timesheet.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Общие стадии агрегации для списка табеля и отчёта по проектам.
///
/// Почему это отдельный класс. Отчёт по ТЗ обязан считаться в базе, а правила
/// (выбор ставки на дату, стоимость, порог переработки) живут в домене на C#.
/// То есть одно и то же правило существует в двух реализациях — это главный
/// архитектурный риск проекта, зафиксированный в NOTES.md, п. 1.1. Держать
/// вторую реализацию в одном месте, а не копировать по обработчикам, —
/// минимум, который снижает риск разъезда. Пороговые значения берутся из тех
/// же доменных констант, что и в C#.
/// </summary>
public static class TimesheetPipeline
{
    /// <summary>
    /// Отбор записей за месяц по полуинтервалу [начало, начало следующего).
    ///
    /// Именно диапазон, а не $year/$month от поля: вычисляемое выражение над
    /// полем не может использовать индекс. Проверено explain: диапазон даёт
    /// IXSCAN, $month — COLLSCAN.
    /// </summary>
    public static BsonDocument MatchMonth(int year, int month, string? employeeId, string? projectId)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        var match = new BsonDocument
        {
            { "date", new BsonDocument { { "$gte", from }, { "$lt", to } } }
        };

        if (!string.IsNullOrWhiteSpace(employeeId)) match["employeeId"] = employeeId;
        if (!string.IsNullOrWhiteSpace(projectId)) match["projectId"] = projectId;

        return new BsonDocument("$match", match);
    }

    /// <summary>
    /// Подтягивает сотрудника, выбирает ставку, действовавшую на дату записи,
    /// и считает стоимость.
    ///
    /// Зеркало доменного правила Employee.RateAt: из истории берутся ставки
    /// с from &lt;= date и выбирается с максимальной from.
    ///
    /// $reduce, а не $sortArray: последний требует MongoDB 5.2+, а $reduce
    /// доступен с 3.4. Нижняя граница для пайплайна в целом — 4.2 из-за
    /// $round и $set.
    /// </summary>
    public static IEnumerable<BsonDocument> ResolveRateAndAmount()
    {
        yield return new BsonDocument("$lookup", new BsonDocument
        {
            { "from", CollectionNames.Employees },
            { "localField", "employeeId" },
            { "foreignField", "_id" },
            { "as", "employee" }
        });

        // preserveNullAndEmptyArrays: висячая ссылка на удалённого сотрудника
        // не должна ронять весь отчёт. В MongoDB нет внешних ключей, поэтому
        // это вопрос времени, а не гипотеза.
        yield return new BsonDocument("$unwind", new BsonDocument
        {
            { "path", "$employee" },
            { "preserveNullAndEmptyArrays", true }
        });

        yield return new BsonDocument("$set", new BsonDocument("appliedRate",
            new BsonDocument("$let", new BsonDocument
            {
                {
                    "vars", new BsonDocument("applicable",
                        new BsonDocument("$filter", new BsonDocument
                        {
                            { "input", new BsonDocument("$ifNull", new BsonArray { "$employee.rates", new BsonArray() }) },
                            { "as", "rate" },
                            { "cond", new BsonDocument("$lte", new BsonArray { "$$rate.from", "$date" }) }
                        }))
                },
                {
                    "in", new BsonDocument("$reduce", new BsonDocument
                    {
                        { "input", "$$applicable" },
                        { "initialValue", BsonNull.Value },
                        {
                            "in", new BsonDocument("$cond", new BsonArray
                            {
                                new BsonDocument("$or", new BsonArray
                                {
                                    new BsonDocument("$eq", new BsonArray { "$$value", BsonNull.Value }),
                                    new BsonDocument("$gt", new BsonArray { "$$this.from", "$$value.from" })
                                }),
                                "$$this",
                                "$$value"
                            })
                        }
                    })
                }
            })));

        // Стоимость округляется на уровне записи, а не на итоге: пользователь
        // видит стоимость каждой строки, и сумма обязана сходиться с тем, что
        // он видит (решение зафиксировано в NOTES.md).
        yield return new BsonDocument("$set", new BsonDocument
        {
            { "rate", new BsonDocument("$ifNull", new BsonArray { "$appliedRate.value", BsonNull.Value }) },
            {
                "amount", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$appliedRate", BsonNull.Value }),
                    new BsonDecimal128(0m),
                    new BsonDocument("$round", new BsonArray
                    {
                        new BsonDocument("$multiply", new BsonArray
                        {
                            new BsonDocument("$toDecimal", "$hours"),
                            new BsonDocument("$toDecimal", "$appliedRate.value")
                        }),
                        2
                    })
                })
            }
        });
    }

    /// <summary>
    /// Считает суммарные часы сотрудника за дату записи по всем проектам и
    /// выставляет признак переработки.
    ///
    /// Отдельный $lookup в ту же коллекцию, потому что сумма за день должна
    /// учитывать все проекты — даже те, что отфильтрованы из выдачи фильтром
    /// по проекту. Иначе при просмотре одного проекта переработка исчезала бы
    /// с экрана.
    ///
    /// Стадия применяется уже после $skip/$limit, то есть отрабатывает на
    /// странице, а не на всём месяце. Запрос внутри ложится на индекс
    /// ix_employee_date.
    /// </summary>
    public static IEnumerable<BsonDocument> ResolveOvertime()
    {
        yield return new BsonDocument("$lookup", new BsonDocument
        {
            { "from", CollectionNames.TimeEntries },
            { "let", new BsonDocument { { "e", "$employeeId" }, { "d", "$date" } } },
            {
                "pipeline", new BsonArray
                {
                    new BsonDocument("$match", new BsonDocument("$expr",
                        new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$employeeId", "$$e" }),
                            new BsonDocument("$eq", new BsonArray { "$date", "$$d" })
                        }))),
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", BsonNull.Value },
                        { "total", new BsonDocument("$sum", new BsonDocument("$toDecimal", "$hours")) }
                    })
                }
            },
            { "as", "dayTotal" }
        });

        yield return new BsonDocument("$set", new BsonDocument("dayHours",
            new BsonDocument("$ifNull", new BsonArray
            {
                new BsonDocument("$arrayElemAt", new BsonArray { "$dayTotal.total", 0 }),
                new BsonDecimal128(0m)
            })));

        // Порог берётся из доменной константы, а не зашит числом в пайплайн.
        yield return new BsonDocument("$set", new BsonDocument("isOvertime",
            new BsonDocument("$gt", new BsonArray
            {
                "$dayHours",
                new BsonDecimal128(TimeEntryRules.OvertimeThresholdPerDay)
            })));
    }
}
