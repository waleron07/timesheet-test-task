// Исправленная версия TimesheetReportHandler.cs (часть 1 задания).
//
// Что исправлено по сравнению с оригиналом (подробности — в REVIEW.md):
//   1. Отчёт считается агрегацией на стороне MongoDB. Исчезли выгрузка всей
//      коллекции в память (п. 1), N+1 запросов (п. 4) и .Result (п. 3).
//   2. Ставка выбирается действовавшая НА ДАТУ записи, а не первая в массиве
//      (п. 2) — это ошибка в деньгах, поэтому она вторая по важности.
//   3. CancellationToken прокинут в драйвер (п. 5).
//   4. Нет NRE на отсутствующем сотруднике/проекте/ставке (п. 6): $lookup с
//      preserveNullAndEmptyArrays, записи без ставки считаются отдельно и
//      видны в ответе, а не молча превращаются в 0.
//   5. Деньги — decimal / Decimal128, double больше нигде не участвует (п. 7).
//   6. Деление на нулевой бюджет не даёт Infinity/NaN — Percent nullable (п. 8).
//   7. Валидация Year/Month (п. 10), UTC-границы месяца (п. 11), имена
//      коллекций — константами (п. 12), добавлен признак риска AtRisk > 80 %.
//
// Оставлено за рамками намеренно (сделано в части 2): вынос документов БД в
// Infrastructure, DTO-слой отдельно от результата хендлера, FluentValidation
// вместо ручной проверки, логирование. Здесь важно было не переписать проект,
// а показать исправление самих дефектов.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Demo.Api.Queries.Reports
{
    public sealed class ProjectReportRow
    {
        public string ProjectId { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectName { get; set; }
        public decimal Hours { get; set; }
        public decimal Amount { get; set; }
        public decimal Budget { get; set; }

        /// <summary>
        /// Процент освоения бюджета. null, если бюджет не задан (0):
        /// в этом случае процент не определён, а не «бесконечность».
        /// </summary>
        public decimal? Percent { get; set; }

        public bool Overspent { get; set; }

        /// <summary>Риск: освоено больше 80 % бюджета (требование ТЗ).</summary>
        public bool AtRisk { get; set; }

        /// <summary>
        /// Записи, для которых не нашлось ставки, действовавшей на их дату.
        /// Такие записи входят в часы, но не в стоимость. Показываем число
        /// явно: тихо занулять деньги в отчёте опаснее, чем упасть.
        /// </summary>
        public int EntriesWithoutRate { get; set; }
    }

    public sealed class ProjectReport
    {
        public IReadOnlyList<ProjectReportRow> Rows { get; set; } = new List<ProjectReportRow>();

        /// <summary>Итоговая строка (требование ТЗ).</summary>
        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class GetProjectReportQuery : IRequest<ProjectReport>
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public sealed class TimesheetReportHandler : IRequestHandler<GetProjectReportQuery, ProjectReport>
    {
        private const string TimeEntriesCollection = "time_entries";
        private const string EmployeesCollection = "employees";
        private const string ProjectsCollection = "projects";

        // Порог риска и перерасхода из ТЗ.
        private const decimal RiskThresholdPercent = 80m;
        private const decimal OverspentThresholdPercent = 100m;

        private readonly IMongoDatabase _db;

        public TimesheetReportHandler(IMongoDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<ProjectReport> Handle(GetProjectReportQuery request, CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // В боевом коде это FluentValidation-валидатор на запрос: валидация
            // формата отделена от бизнес-правил. Здесь — минимальная проверка,
            // чтобы не получить ArgumentOutOfRangeException ниже и 500 в ответ.
            if (request.Month < 1 || request.Month > 12)
                throw new ArgumentOutOfRangeException(nameof(request.Month), request.Month, "Месяц должен быть в диапазоне 1..12.");
            if (request.Year < 2000 || request.Year > 2100)
                throw new ArgumentOutOfRangeException(nameof(request.Year), request.Year, "Год должен быть в диапазоне 2000..2100.");

            // Полуинтервал [from, to) вместо $year/$month от поля: такой предикат
            // использует индекс { date: 1 }, а вычисление month() от каждого
            // документа — нет. Kind = Utc, чтобы границы месяца не съехали на
            // смещение таймзоны сервера.
            var from = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddMonths(1);

            var pipeline = BuildPipeline(from, to);

            var collection = _db.GetCollection<BsonDocument>(TimeEntriesCollection);

            using var cursor = await collection
                .AggregateAsync(pipeline, new AggregateOptions { AllowDiskUse = true }, token)
                .ConfigureAwait(false);

            var documents = await cursor.ToListAsync(token).ConfigureAwait(false);

            var rows = documents.Select(MapRow).ToList();

            return new ProjectReport
            {
                Rows = rows,
                TotalHours = rows.Sum(r => r.Hours),
                TotalAmount = rows.Sum(r => r.Amount)
            };
        }

        /// <summary>
        /// Пайплайн отчёта. Вся работа — на стороне MongoDB: в C# приезжает
        /// по одному документу на проект, а не миллионы записей табеля.
        ///
        /// Используемые индексы (создаются явно, обоснование — в NOTES.md):
        ///   time_entries: { date: 1, projectId: 1 }  — под $match + $group
        ///   employees:    { _id: 1 }                 — под $lookup (по умолчанию)
        ///   projects:     { _id: 1 }                 — под $lookup (по умолчанию)
        /// </summary>
        private static PipelineDefinition<BsonDocument, BsonDocument> BuildPipeline(DateTime from, DateTime to)
        {
            var stages = new List<BsonDocument>
            {
                // 1. Отбираем только нужный месяц — по индексируемому диапазону.
                new BsonDocument("$match", new BsonDocument("date",
                    new BsonDocument
                    {
                        { "$gte", from },
                        { "$lt", to }
                    })),

                // 2. Подтягиваем сотрудника ради истории ставок.
                //    preserveNullAndEmptyArrays: висячая ссылка (сотрудник удалён)
                //    не должна ронять весь отчёт — см. п. 6 ревью.
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", EmployeesCollection },
                    { "localField", "employeeId" },
                    { "foreignField", "_id" },
                    { "as", "employee" }
                }),
                new BsonDocument("$unwind", new BsonDocument
                {
                    { "path", "$employee" },
                    { "preserveNullAndEmptyArrays", true }
                }),

                // 3. Ключевое место: ставка, ДЕЙСТВОВАВШАЯ НА ДАТУ ЗАПИСИ.
                //    Берём из истории все ставки с from <= date и выбираем
                //    ту, у которой from максимальна.
                //
                //    $reduce, а не $sortArray: $sortArray требует MongoDB 5.2+,
                //    а $reduce есть с 3.4. Результат идентичен, но пайплайн
                //    переносится на более старые кластеры. Нижняя граница версии
                //    для всего пайплайна — 4.2 из-за $round и $set.
                new BsonDocument("$set", new BsonDocument("appliedRate",
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
                    }))),

                // 4. Стоимость записи = часы × ставка, округление до копеек.
                //    Округляем на уровне записи, а не на итоге: пользователь видит
                //    стоимость каждой строки табеля, и итог обязан быть суммой
                //    видимых строк (осознанное решение, зафиксировано в NOTES.md).
                //    Всё в decimal: $toDecimal защищает и от того, что в старых
                //    документах часы лежат как double.
                new BsonDocument("$set", new BsonDocument
                {
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
                    },
                    {
                        "withoutRate", new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$appliedRate", BsonNull.Value }), 1, 0
                        })
                    }
                }),

                // 5. Свёртка по проекту.
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$projectId" },
                    { "hours", new BsonDocument("$sum", new BsonDocument("$toDecimal", "$hours")) },
                    { "amount", new BsonDocument("$sum", "$amount") },
                    { "entriesWithoutRate", new BsonDocument("$sum", "$withoutRate") }
                }),

                // 6. Имя и бюджет проекта — один $lookup на проект, а не на запись.
                new BsonDocument("$lookup", new BsonDocument
                {
                    { "from", ProjectsCollection },
                    { "localField", "_id" },
                    { "foreignField", "_id" },
                    { "as", "project" }
                }),
                new BsonDocument("$unwind", new BsonDocument
                {
                    { "path", "$project" },
                    { "preserveNullAndEmptyArrays", true }
                }),

                new BsonDocument("$project", new BsonDocument
                {
                    { "_id", 1 },
                    { "hours", 1 },
                    { "amount", 1 },
                    { "entriesWithoutRate", 1 },
                    { "projectCode", new BsonDocument("$ifNull", new BsonArray { "$project.code", BsonNull.Value }) },
                    // Проект не найден — показываем это, а не падаем с NRE.
                    { "projectName", new BsonDocument("$ifNull", new BsonArray { "$project.name", "Проект не найден" }) },
                    { "budget", new BsonDocument("$toDecimal", new BsonDocument("$ifNull", new BsonArray { "$project.budget", 0 })) }
                }),

                // 7. Сортировка — тоже в базе.
                new BsonDocument("$sort", new BsonDocument("projectName", 1))
            };

            return PipelineDefinition<BsonDocument, BsonDocument>.Create(stages);
        }

        private static ProjectReportRow MapRow(BsonDocument doc)
        {
            var amount = ToDecimal(doc, "amount");
            var budget = ToDecimal(doc, "budget");

            // Бюджет 0 (проект только завели) — процент не определён.
            // В double это дало бы Infinity и невалидный JSON, в decimal —
            // DivideByZeroException. Поэтому проверка обязательна.
            decimal? percent = budget > 0m
                ? Math.Round(amount / budget * 100m, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;

            return new ProjectReportRow
            {
                ProjectId = doc.GetValue("_id", BsonNull.Value).ToString(),
                ProjectCode = AsNullableString(doc, "projectCode"),
                ProjectName = AsNullableString(doc, "projectName"),
                Hours = ToDecimal(doc, "hours"),
                Amount = amount,
                Budget = budget,
                Percent = percent,
                Overspent = percent.HasValue && percent.Value > OverspentThresholdPercent,
                AtRisk = percent.HasValue && percent.Value > RiskThresholdPercent,
                EntriesWithoutRate = doc.GetValue("entriesWithoutRate", 0).ToInt32()
            };
        }

        private static decimal ToDecimal(BsonDocument doc, string field)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsBsonNull ? 0m : value.ToDecimal();
        }

        private static string AsNullableString(BsonDocument doc, string field)
        {
            var value = doc.GetValue(field, BsonNull.Value);
            return value.IsBsonNull ? null : value.AsString;
        }
    }
}
