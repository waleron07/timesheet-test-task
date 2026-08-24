using MediatR;
using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Contracts;
using Timesheet.Api.Features.Dictionaries;

namespace Timesheet.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class DictionariesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DictionariesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("employees")]
    public Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(CancellationToken token) =>
        _mediator.Send(new GetEmployeesQuery(), token);

    [HttpGet("projects")]
    public Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken token) =>
        _mediator.Send(new GetProjectsQuery(), token);
}
