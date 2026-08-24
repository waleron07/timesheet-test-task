using FluentValidation;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.Reports;

public sealed record GetProjectReportQuery(int Year, int Month) : IRequest<ProjectReportDto>;

public sealed class GetProjectReportQueryValidator : AbstractValidator<GetProjectReportQuery>
{
    public GetProjectReportQueryValidator()
    {
        RuleFor(q => q.Year).InclusiveBetween(2000, 2100).WithMessage("Год должен быть в диапазоне 2000–2100.");
        RuleFor(q => q.Month).InclusiveBetween(1, 12).WithMessage("Месяц должен быть в диапазоне 1–12.");
    }
}

/// <summary>
/// Отчёт по проектам за месяц — целиком агрегацией на стороне MongoDB.
///
/// В C# приезжает по одному документу на проект, а не миллионы записей табеля:
/// требование ТЗ и главная претензия к исходному коду из части 1.
/// </summary>
public sealed class GetProjectReportQueryHandler : IRequestHandler<GetProjectReportQuery, ProjectReportDto>
{
    private readonly TimesheetCollections _collections;

    public GetProjectReportQueryHandler(TimesheetCollections collections)
    {
        _collections = collections;
    }

    public async Task<ProjectReportDto> Handle(GetProjectReportQuery request, CancellationToken token)
    {
        var stages = new List<BsonDocument>
        {
            TimesheetPipeline.MatchMonth(request.Year, request.Month, employeeId: null, projectId: null)
        };

        stages.AddRange(TimesheetPipeline.ResolveRateAndAmount());

        stages.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$projectId" },
            { "hours", new BsonDocument("$sum", new BsonDocument("$toDecimal", "$hours")) },
            { "amount", new BsonDocument("$sum", "$amount") },
            {
                // Записи без применимой ставки не должны тихо превращаться в
                // ноль: часы у них есть, денег нет. Считаем их отдельно, чтобы
                // расхождение было видно, а не пряталось в итогах.
                "entriesWithoutRate", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$appliedRate", BsonNull.Value }), 1, 0
                }))
            }
        }));

        stages.Add(new BsonDocument("$lookup", new BsonDocument
        {
            { "from", CollectionNames.Projects },
            { "localField", "_id" },
            { "foreignField", "_id" },
            { "as", "project" }
        }));

        stages.Add(new BsonDocument("$unwind", new BsonDocument
        {
            { "path", "$project" },
            { "preserveNullAndEmptyArrays", true }
        }));

        // Сортировка тоже в базе.
        stages.Add(new BsonDocument("$sort", new BsonDocument("project.code", 1)));

        var cursor = await _collections.TimeEntries
            .AggregateAsync<BsonDocument>(stages, new AggregateOptions { AllowDiskUse = true }, token)
            .ConfigureAwait(false);

        var documents = await cursor.ToListAsync(token).ConfigureAwait(false);

        var rows = documents.Select(MapRow).ToList();

        // Итоговая строка суммируется в C# по уже агрегированным строкам —
        // их столько, сколько проектов с трудозатратами за месяц, то есть
        // единицы. Это не выгрузка записей в память.
        return new ProjectReportDto(rows, rows.Sum(r => r.Hours), rows.Sum(r => r.Amount));
    }

    private static ProjectReportRowDto MapRow(BsonDocument doc)
    {
        var amount = BsonValues.ToDecimal(doc, "amount");
        var budget = BsonValues.ToDecimal(doc, "project.budget");

        // Пороги и формула процента берутся из домена, а не дублируются здесь.
        var percent = ReportRules.BudgetPercent(amount, budget);

        return new ProjectReportRowDto(
            ProjectId: doc.GetValue("_id", BsonNull.Value).ToString() ?? string.Empty,
            ProjectCode: BsonValues.ToStringOr(doc, "project.code", "—"),
            ProjectName: BsonValues.ToStringOr(doc, "project.name", "Проект не найден"),
            Hours: BsonValues.ToDecimal(doc, "hours"),
            Amount: amount,
            Budget: budget,
            Percent: percent,
            Overspent: ReportRules.IsOverspent(percent),
            AtRisk: ReportRules.IsAtRisk(percent),
            EntriesWithoutRate: doc.GetValue("entriesWithoutRate", 0).ToInt32());
    }
}
