using MediatR;
using MongoDB.Driver;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.TimeEntries;

public sealed record DeleteTimeEntryCommand(string Id) : IRequest;

public sealed class DeleteTimeEntryCommandHandler : IRequestHandler<DeleteTimeEntryCommand>
{
    private readonly TimesheetCollections _collections;
    private readonly TimeEntryGuard _guard;

    public DeleteTimeEntryCommandHandler(TimesheetCollections collections, TimeEntryGuard guard)
    {
        _collections = collections;
        _guard = guard;
    }

    public async Task Handle(DeleteTimeEntryCommand request, CancellationToken token)
    {
        var entry = await _guard.RequireEntryAsync(request.Id, token).ConfigureAwait(false);

        await _guard.EnsurePeriodOpenAsync(entry.Date, token).ConfigureAwait(false);

        await _collections.TimeEntries
            .DeleteOneAsync(e => e.Id == request.Id, token)
            .ConfigureAwait(false);
    }
}
