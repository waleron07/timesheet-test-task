namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Имена коллекций в одном месте. Опечатка в строковом литерале не даёт ошибки:
/// MongoDB молча создаёт новую пустую коллекцию, и запрос возвращает пустоту —
/// именно этот дефект отмечен в REVIEW.md, п. 12.
/// </summary>
public static class CollectionNames
{
    public const string TimeEntries = "time_entries";
    public const string Employees = "employees";
    public const string Projects = "projects";
    public const string ClosedPeriods = "closed_periods";
}
