using MediatR;
using Microsoft.AspNetCore.Mvc;
using Timesheet.Api.Contracts;
using Timesheet.Api.Features.TimeEntries;

namespace Timesheet.Api.Controllers;

/// <summary>
/// Контроллер намеренно тонкий: разбор запроса, вызов MediatR, код ответа.
/// Ни одного бизнес-правила здесь нет.
///
/// Про методы: создание — PUT, изменение — POST /{id}. Это обратно привычной
/// семантике HTTP, но так задано в ТЗ, и вероятная причина — совместимость с
/// существующим фронтом. Реализовано как написано, без самовольных
/// «улучшений» (NOTES.md, п. 1.6).
/// </summary>
[ApiController]
[Route("api/time-entries")]
public sealed class TimeEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TimeEntriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public Task<PagedTimeEntriesDto> GetAsync(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? employeeId,
        [FromQuery] string? projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken token = default) =>
        _mediator.Send(new GetTimeEntriesQuery(year, month, employeeId, projectId, page, pageSize), token);

    [HttpPut]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateTimeEntryRequest request, CancellationToken token)
    {
        var id = await _mediator.Send(new CreateTimeEntryCommand(
            request.EmployeeId, request.ProjectId, request.Date, request.Hours, request.Comment), token);

        return Created($"/api/time-entries/{id}", new { id });
    }

    [HttpPost("{id}")]
    public async Task<IActionResult> UpdateAsync(
        string id, [FromBody] UpdateTimeEntryRequest request, CancellationToken token)
    {
        await _mediator.Send(new UpdateTimeEntryCommand(
            id, request.EmployeeId, request.ProjectId, request.Date,
            request.Hours, request.Comment, request.Version), token);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken token)
    {
        await _mediator.Send(new DeleteTimeEntryCommand(id), token);

        return NoContent();
    }
}
