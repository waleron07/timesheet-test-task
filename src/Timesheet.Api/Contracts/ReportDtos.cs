namespace Timesheet.Api.Contracts;

public sealed record ProjectReportRowDto(
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    decimal Hours,
    decimal Amount,
    decimal Budget,
    // Percent = null, если бюджет не задан: процент от нулевой базы не определён.
    decimal? Percent,
    bool Overspent,
    bool AtRisk,
    // Записи, для которых не нашлось ставки на их дату: часы есть, денег нет.
    int EntriesWithoutRate);

public sealed record ProjectReportDto(
    IReadOnlyList<ProjectReportRowDto> Rows,
    decimal TotalHours,
    decimal TotalAmount);
