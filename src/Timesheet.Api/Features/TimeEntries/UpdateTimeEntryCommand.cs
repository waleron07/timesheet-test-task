using FluentValidation;
using MediatR;
using MongoDB.Driver;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.TimeEntries;

public sealed record UpdateTimeEntryCommand(
    string Id, string EmployeeId, string ProjectId, DateOnly Date, decimal Hours, string? Comment, int Version)
    : IRequest;

public sealed class UpdateTimeEntryCommandValidator : AbstractValidator<UpdateTimeEntryCommand>
{
    public UpdateTimeEntryCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeId).NotEmpty().WithMessage("Выберите сотрудника.");
        RuleFor(c => c.ProjectId).NotEmpty().WithMessage("Выберите проект.");
        RuleFor(c => c.Version).GreaterThan(0).WithMessage("Не передана версия записи.");
        RuleFor(c => c.Hours)
            .GreaterThan(0m).WithMessage("Количество часов должно быть больше нуля.")
            .LessThanOrEqualTo(TimeEntryRules.MaxHoursPerEntry)
            .WithMessage($"Количество часов не может превышать {TimeEntryRules.MaxHoursPerEntry:0.##}.")
            .Must(h => h % TimeEntryRules.HoursStep == 0m).WithMessage("Количество часов должно быть кратно 0,5.");
        RuleFor(c => c.Comment).MaximumLength(500).WithMessage("Комментарий не длиннее 500 символов.");
    }
}

public sealed class UpdateTimeEntryCommandHandler : IRequestHandler<UpdateTimeEntryCommand>
{
    private readonly TimesheetCollections _collections;
    private readonly TimeEntryGuard _guard;

    public UpdateTimeEntryCommandHandler(TimesheetCollections collections, TimeEntryGuard guard)
    {
        _collections = collections;
        _guard = guard;
    }

    public async Task Handle(UpdateTimeEntryCommand request, CancellationToken token)
    {
        var existing = await _guard.RequireEntryAsync(request.Id, token).ConfigureAwait(false);

        // Период, где запись лежит сейчас: закрытый месяц нельзя менять и
        // «на выход» — иначе часы утекали бы из него простой сменой даты
        // (NOTES.md, п. 1.4).
        await _guard.EnsurePeriodOpenAsync(existing.Date, token).ConfigureAwait(false);

        // Прежние часы этой записи исключаются из суммы дня.
        await _guard.ValidateAsync(
            request.EmployeeId, request.ProjectId, request.Date, request.Hours,
            excludeEntryId: request.Id, token).ConfigureAwait(false);

        // Оптимистичная блокировка: фильтр включает версию, с которой запись
        // открывали. Если её уже изменили, MatchedCount будет нулём — чужая
        // правка не затирается молча.
        var filter = Builders<TimeEntry>.Filter.Eq(e => e.Id, request.Id)
                     & Builders<TimeEntry>.Filter.Eq(e => e.Version, request.Version);

        var update = Builders<TimeEntry>.Update
            .Set(e => e.EmployeeId, request.EmployeeId)
            .Set(e => e.ProjectId, request.ProjectId)
            .Set(e => e.Date, request.Date)
            .Set(e => e.Hours, request.Hours)
            .Set(e => e.Comment, request.Comment)
            .Set(e => e.UpdatedAt, DateTime.UtcNow)
            .Set(e => e.UpdatedBy, "system")
            .Inc(e => e.Version, 1);

        var result = await _collections.TimeEntries
            .UpdateOneAsync(filter, update, cancellationToken: token)
            .ConfigureAwait(false);

        if (result.MatchedCount == 0)
            throw new BusinessRuleException(ErrorCodes.ConcurrentModification,
                "Запись была изменена другим пользователем, пока вы её редактировали. " +
                "Обновите страницу и внесите изменения заново.");
    }
}
