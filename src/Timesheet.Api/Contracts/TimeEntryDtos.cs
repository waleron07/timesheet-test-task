namespace Timesheet.Api.Contracts;

/// <summary>Строка списка табеля. Контракт API, отдельный от модели БД.</summary>
public sealed record TimeEntryDto(
    string Id,
    string EmployeeId,
    string EmployeeName,
    string ProjectId,
    string ProjectCode,
    string ProjectName,
    DateOnly Date,
    decimal Hours,
    decimal? Rate,
    decimal Amount,
    string? Comment,
    bool IsOvertime,
    decimal DayHours,
    int Version);

/// <summary>
/// Страница списка. Итоги считаются по всей выборке под фильтрами, а не по
/// видимой странице: итог по странице менялся бы при смене pageSize и не
/// отвечал бы на вопрос «сколько всего за месяц» (NOTES.md, п. 1.5).
/// </summary>
public sealed record PagedTimeEntriesDto(
    IReadOnlyList<TimeEntryDto> Items,
    long TotalCount,
    int Page,
    int PageSize,
    decimal TotalHours,
    decimal TotalAmount);

public sealed record CreateTimeEntryRequest(
    string EmployeeId,
    string ProjectId,
    DateOnly Date,
    decimal Hours,
    string? Comment);

/// <summary>
/// Изменение записи. Version — версия, с которой запись была открыта на
/// редактирование; если в базе она уже другая, сохранение отклоняется.
/// </summary>
public sealed record UpdateTimeEntryRequest(
    string EmployeeId,
    string ProjectId,
    DateOnly Date,
    decimal Hours,
    string? Comment,
    int Version);
