using MediatR;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.Dictionaries;

public sealed record GetEmployeesQuery : IRequest<IReadOnlyList<EmployeeDto>>;

public sealed class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    private readonly TimesheetCollections _collections;

    public GetEmployeesQueryHandler(TimesheetCollections collections) => _collections = collections;

    public async Task<IReadOnlyList<EmployeeDto>> Handle(GetEmployeesQuery request, CancellationToken token)
    {
        var employees = await _collections.Employees
            .Find(FilterDefinition<Employee>.Empty)
            .SortBy(e => e.FullName)
            .ToListAsync(token)
            .ConfigureAwait(false);

        // Справочник для выпадающего списка: историю ставок наружу не отдаём.
        return employees.Select(e => new EmployeeDto(e.Id, e.FullName, e.Department)).ToList();
    }
}

public sealed record GetProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;

public sealed class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly TimesheetCollections _collections;

    public GetProjectsQueryHandler(TimesheetCollections collections) => _collections = collections;

    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken token)
    {
        var projects = await _collections.Projects
            .Find(FilterDefinition<Project>.Empty)
            .SortBy(p => p.Code)
            .ToListAsync(token)
            .ConfigureAwait(false);

        return projects
            .Select(p => new ProjectDto(p.Id, p.Code, p.Name, p.Budget, p.StartDate, p.EndDate))
            .ToList();
    }
}
