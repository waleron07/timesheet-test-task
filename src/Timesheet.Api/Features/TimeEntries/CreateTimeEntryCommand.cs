using FluentValidation;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Contracts;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.TimeEntries;

public sealed record CreateTimeEntryCommand(
    string EmployeeId, string ProjectId, DateOnly Date, decimal Hours, string? Comment) : IRequest<string>;

/// <summary>
/// Валидация формата: проверяет только то, что видно в самом запросе.
/// Кратность 0,5 и границы часов дублируются доменом — здесь для того, чтобы
/// пользователь получил ошибку по полю, не дожидаясь похода в базу.
/// </summary>
public sealed class CreateTimeEntryCommandValidator : AbstractValidator<CreateTimeEntryCommand>
{
    public CreateTimeEntryCommandValidator()
    {
        RuleFor(c => c.EmployeeId).NotEmpty().WithMessage("Выберите сотрудника.");
        RuleFor(c => c.ProjectId).NotEmpty().WithMessage("Выберите проект.");
        RuleFor(c => c.Hours)
            .GreaterThan(0m).WithMessage("Количество часов должно быть больше нуля.")
            .LessThanOrEqualTo(TimeEntryRules.MaxHoursPerEntry)
            .WithMessage($"Количество часов не может превышать {TimeEntryRules.MaxHoursPerEntry:0.##}.")
            .Must(h => h % TimeEntryRules.HoursStep == 0m).WithMessage("Количество часов должно быть кратно 0,5.");
        RuleFor(c => c.Comment).MaximumLength(500).WithMessage("Комментарий не длиннее 500 символов.");
    }
}

public sealed class CreateTimeEntryCommandHandler : IRequestHandler<CreateTimeEntryCommand, string>
{
    private readonly TimesheetCollections _collections;
    private readonly TimeEntryGuard _guard;

    public CreateTimeEntryCommandHandler(TimesheetCollections collections, TimeEntryGuard guard)
    {
        _collections = collections;
        _guard = guard;
    }

    public async Task<string> Handle(CreateTimeEntryCommand request, CancellationToken token)
    {
        await _guard.ValidateAsync(
            request.EmployeeId, request.ProjectId, request.Date, request.Hours,
            excludeEntryId: null, token).ConfigureAwait(false);

        var entry = new TimeEntry
        {
            Id = ObjectId.GenerateNewId().ToString(),
            EmployeeId = request.EmployeeId,
            ProjectId = request.ProjectId,
            Date = request.Date,
            Hours = request.Hours,
            Comment = request.Comment,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            // Аутентификации в задании нет, поэтому автор фиксируется условно:
            // поле в модели есть, заполнять его будет слой авторизации.
            CreatedBy = "system"
        };

        await _collections.TimeEntries.InsertOneAsync(entry, cancellationToken: token).ConfigureAwait(false);

        return entry.Id;
    }
}
