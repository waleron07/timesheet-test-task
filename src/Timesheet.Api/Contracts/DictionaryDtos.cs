namespace Timesheet.Api.Contracts;

public sealed record EmployeeDto(string Id, string FullName, string Department);

public sealed record ProjectDto(
    string Id, string Code, string Name, decimal Budget, DateOnly StartDate, DateOnly? EndDate);

public sealed record ClosedPeriodDto(int Year, int Month);
