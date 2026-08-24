using FluentValidation;
using MediatR;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.Periods;

public sealed record ClosePeriodCommand(int Year, int Month) : IRequest;

public sealed record OpenPeriodCommand(int Year, int Month) : IRequest;

public sealed record GetClosedPeriodsQuery : IRequest<IReadOnlyList<ClosedPeriodDto>>;

public sealed class ClosePeriodCommandValidator : AbstractValidator<ClosePeriodCommand>
{
    public ClosePeriodCommandValidator()
    {
        RuleFor(c => c.Year).InclusiveBetween(2000, 2100).WithMessage("Год должен быть в диапазоне 2000–2100.");
        RuleFor(c => c.Month).InclusiveBetween(1, 12).WithMessage("Месяц должен быть в диапазоне 1–12.");
    }
}

public sealed class OpenPeriodCommandValidator : AbstractValidator<OpenPeriodCommand>
{
    public OpenPeriodCommandValidator()
    {
        RuleFor(c => c.Year).InclusiveBetween(2000, 2100).WithMessage("Год должен быть в диапазоне 2000–2100.");
        RuleFor(c => c.Month).InclusiveBetween(1, 12).WithMessage("Месяц должен быть в диапазоне 1–12.");
    }
}

public sealed class ClosePeriodCommandHandler : IRequestHandler<ClosePeriodCommand>
{
    private readonly TimesheetCollections _collections;

    public ClosePeriodCommandHandler(TimesheetCollections collections) => _collections = collections;

    public async Task Handle(ClosePeriodCommand request, CancellationToken token)
    {
        // Upsert, а не Insert: повторное закрытие уже закрытого месяца —
        // не ошибка, а тот же самый результат (NOTES.md, п. 1.7).
        // Уникальный индекс по (year, month) не даст появиться дублю при гонке.
        var filter = Builders<ClosedPeriod>.Filter.Eq(p => p.Year, request.Year)
                     & Builders<ClosedPeriod>.Filter.Eq(p => p.Month, request.Month);

        var update = Builders<ClosedPeriod>.Update
            .SetOnInsert(p => p.Id, $"{request.Year:D4}-{request.Month:D2}")
            .SetOnInsert(p => p.Year, request.Year)
            .SetOnInsert(p => p.Month, request.Month)
            .Set(p => p.ClosedAt, DateTime.UtcNow)
            .Set(p => p.ClosedBy, "system");

        await _collections.ClosedPeriods
            .UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, token)
            .ConfigureAwait(false);
    }
}

public sealed class OpenPeriodCommandHandler : IRequestHandler<OpenPeriodCommand>
{
    private readonly TimesheetCollections _collections;

    public OpenPeriodCommandHandler(TimesheetCollections collections) => _collections = collections;

    public async Task Handle(OpenPeriodCommand request, CancellationToken token)
    {
        // Открытие не закрытого месяца тоже не ошибка: результат тот же.
        await _collections.ClosedPeriods
            .DeleteOneAsync(p => p.Year == request.Year && p.Month == request.Month, token)
            .ConfigureAwait(false);
    }
}

public sealed class GetClosedPeriodsQueryHandler : IRequestHandler<GetClosedPeriodsQuery, IReadOnlyList<ClosedPeriodDto>>
{
    private readonly TimesheetCollections _collections;

    public GetClosedPeriodsQueryHandler(TimesheetCollections collections) => _collections = collections;

    public async Task<IReadOnlyList<ClosedPeriodDto>> Handle(GetClosedPeriodsQuery request, CancellationToken token)
    {
        var periods = await _collections.ClosedPeriods
            .Find(FilterDefinition<ClosedPeriod>.Empty)
            .ToListAsync(token)
            .ConfigureAwait(false);

        // Нужен фронту, чтобы показывать замок на закрытом месяце заранее,
        // а не только после отказа при сохранении.
        return periods
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .Select(p => new ClosedPeriodDto(p.Year, p.Month))
            .ToList();
    }
}
