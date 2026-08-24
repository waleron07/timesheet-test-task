using MediatR;
using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Contracts;
using Timesheet.Api.Features.Periods;

namespace Timesheet.Api.Controllers;

[ApiController]
[Route("api/periods")]
public sealed class PeriodsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PeriodsController(IMediator mediator) => _mediator = mediator;

    public sealed record PeriodRequest(int Year, int Month);

    [HttpGet]
    public Task<IReadOnlyList<ClosedPeriodDto>> GetClosedAsync(CancellationToken token) =>
        _mediator.Send(new GetClosedPeriodsQuery(), token);

    [HttpPost("close")]
    public async Task<IActionResult> CloseAsync([FromBody] PeriodRequest request, CancellationToken token)
    {
        await _mediator.Send(new ClosePeriodCommand(request.Year, request.Month), token);
        return NoContent();
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenAsync([FromBody] PeriodRequest request, CancellationToken token)
    {
        await _mediator.Send(new OpenPeriodCommand(request.Year, request.Month), token);
        return NoContent();
    }
}
