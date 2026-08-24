using FluentValidation;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Infrastructure;

namespace Timesheet.Api.Features.TimeEntries;

public sealed record GetTimeEntriesQuery(
    int Year, int Month, string? EmployeeId, string? ProjectId, int Page, int PageSize)
    : IRequest<PagedTimeEntriesDto>;

public sealed class GetTimeEntriesQueryValidator : AbstractValidator<GetTimeEntriesQuery>
{
    public GetTimeEntriesQueryValidator()
    {
        RuleFor(q => q.Year).InclusiveBetween(2000, 2100).WithMessage("Год должен быть в диапазоне 2000–2100.");
        RuleFor(q => q.Month).InclusiveBetween(1, 12).WithMessage("Месяц должен быть в диапазоне 1–12.");
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Номер страницы начинается с 1.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200).WithMessage("Размер страницы — от 1 до 200.");
    }
}

/// <summary>
/// Постраничный список записей за месяц.
///
/// Пагинация выполняется в базе через $facet: страница и агрегаты считаются
/// за одну поездку. Выгрузки всей коллекции в память нет нигде — ровно тот
/// дефект, за который разобран исходный код в REVIEW.md.
/// </summary>
public sealed class GetTimeEntriesQueryHandler : IRequestHandler<GetTimeEntriesQuery, PagedTimeEntriesDto>
{
    private readonly TimesheetCollections _collections;

    public GetTimeEntriesQueryHandler(TimesheetCollections collections)
    {
        _collections = collections;
    }

    public async Task<PagedTimeEntriesDto> Handle(GetTimeEntriesQuery request, CancellationToken token)
    {
        var stages = new List<BsonDocument>
        {
            TimesheetPipeline.MatchMonth(request.Year, request.Month, request.EmployeeId, request.ProjectId)
        };

        // Ставка и стоимость считаются до $facet: итоги по всей выборке
        // требуют стоимости каждой записи, иначе их не просуммировать.
        stages.AddRange(TimesheetPipeline.ResolveRateAndAmount());

        var itemsBranch = new BsonArray
        {
            // Сортировка по дате, затем по _id: без второго ключа порядок
            // записей с одинаковой датой не определён, и одна и та же запись
            // может оказаться на двух страницах либо не попасть ни на одну.
            new BsonDocument("$sort", new BsonDocument { { "date", 1 }, { "_id", 1 } }),
            new BsonDocument("$skip", (request.Page - 1) * request.PageSize),
            new BsonDocument("$limit", request.PageSize)
        };

        // Переработка считается уже на странице, а не на всём месяце.
        foreach (var stage in TimesheetPipeline.ResolveOvertime()) itemsBranch.Add(stage);

        itemsBranch.Add(new BsonDocument("$lookup", new BsonDocument
        {
            { "from", CollectionNames.Projects },
            { "localField", "projectId" },
            { "foreignField", "_id" },
            { "as", "project" }
        }));
        itemsBranch.Add(new BsonDocument("$unwind", new BsonDocument
        {
            { "path", "$project" },
            { "preserveNullAndEmptyArrays", true }
        }));

        stages.Add(new BsonDocument("$facet", new BsonDocument
        {
            { "items", itemsBranch },
            {
                "totals", new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument
                    {
                        { "_id", BsonNull.Value },
                        { "count", new BsonDocument("$sum", 1) },
                        { "hours", new BsonDocument("$sum", new BsonDocument("$toDecimal", "$hours")) },
                        { "amount", new BsonDocument("$sum", "$amount") }
                    })
                }
            }
        }));

        var cursor = await _collections.TimeEntries
            .AggregateAsync<BsonDocument>(stages, new AggregateOptions { AllowDiskUse = true }, token)
            .ConfigureAwait(false);

        var facet = await cursor.FirstOrDefaultAsync(token).ConfigureAwait(false);

        if (facet is null)
            return new PagedTimeEntriesDto(Array.Empty<TimeEntryDto>(), 0, request.Page, request.PageSize, 0m, 0m);

        var items = facet["items"].AsBsonArray.Select(MapItem).ToList();

        var totals = facet["totals"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;

        return new PagedTimeEntriesDto(
            items,
            totals?.GetValue("count", 0).ToInt64() ?? 0,
            request.Page,
            request.PageSize,
            BsonValues.ToDecimal(totals, "hours"),
            BsonValues.ToDecimal(totals, "amount"));
    }

    private static TimeEntryDto MapItem(BsonValue value)
    {
        var doc = value.AsBsonDocument;

        return new TimeEntryDto(
            Id: doc["_id"].AsString,
            EmployeeId: doc.GetValue("employeeId", BsonNull.Value).ToString() ?? string.Empty,
            // Сотрудник или проект могли быть удалены: показываем это явно,
            // а не падаем с NRE, как исходный код из части 1.
            EmployeeName: BsonValues.ToStringOr(doc, "employee.fullName", "Сотрудник не найден"),
            ProjectId: doc.GetValue("projectId", BsonNull.Value).ToString() ?? string.Empty,
            ProjectCode: BsonValues.ToStringOr(doc, "project.code", "—"),
            ProjectName: BsonValues.ToStringOr(doc, "project.name", "Проект не найден"),
            Date: DateOnly.FromDateTime(doc["date"].ToUniversalTime()),
            Hours: BsonValues.ToDecimal(doc, "hours"),
            Rate: BsonValues.ToNullableDecimal(doc, "rate"),
            Amount: BsonValues.ToDecimal(doc, "amount"),
            Comment: BsonValues.ToNullableString(doc, "comment"),
            IsOvertime: doc.GetValue("isOvertime", false).ToBoolean(),
            DayHours: BsonValues.ToDecimal(doc, "dayHours"),
            Version: doc.GetValue("version", 0).ToInt32());
    }
}
