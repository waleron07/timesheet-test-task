using MediatR;
using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Contracts;
using Timesheet.Api.Features.Reports;

namespace Timesheet.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("projects")]
    public Task<ProjectReportDto> GetProjectsAsync(
        [FromQuery] int year, [FromQuery] int month, CancellationToken token) =>
        _mediator.Send(new GetProjectReportQuery(year, month), token);
}
