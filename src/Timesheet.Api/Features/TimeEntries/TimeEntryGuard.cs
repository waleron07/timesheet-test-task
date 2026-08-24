using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api.Infrastructure;
using Timesheet.Domain;

namespace Timesheet.Api.Features.TimeEntries;

/// <summary>
/// Подготовка данных, которых требуют доменные правила, и их прогон.
///
/// Сами правила живут в домене и тестируются без базы; здесь только то, что
/// без базы проверить нельзя: существование сотрудника и проекта, закрытость
/// периода и сумма часов за день. Общий класс для создания и изменения —
/// иначе два обработчика неизбежно разъедутся в наборе проверок.
/// </summary>
public sealed class TimeEntryGuard
{
    private readonly TimesheetCollections _collections;

    public TimeEntryGuard(TimesheetCollections collections)
    {
        _collections = collections;
    }

    public async Task<Employee> RequireEmployeeAsync(string employeeId, CancellationToken token)
    {
        var employee = await _collections.Employees
            .Find(e => e.Id == employeeId)
            .FirstOrDefaultAsync(token)
            .ConfigureAwait(false);

        return employee ?? throw new BusinessRuleException(
            ErrorCodes.EmployeeNotFound, "Сотрудник не найден.");
    }

    public async Task<Project> RequireProjectAsync(string projectId, CancellationToken token)
    {
        var project = await _collections.Projects
            .Find(p => p.Id == projectId)
            .FirstOrDefaultAsync(token)
            .ConfigureAwait(false);

        return project ?? throw new BusinessRuleException(
            ErrorCodes.ProjectNotFound, "Проект не найден.");
    }

    public async Task<TimeEntry> RequireEntryAsync(string id, CancellationToken token)
    {
        var entry = await _collections.TimeEntries
            .Find(e => e.Id == id)
            .FirstOrDefaultAsync(token)
            .ConfigureAwait(false);

        return entry ?? throw new BusinessRuleException(
            ErrorCodes.TimeEntryNotFound, "Запись табеля не найдена.");
    }

    public async Task EnsurePeriodOpenAsync(DateOnly date, CancellationToken token)
    {
        var period = YearMonth.Of(date);

        var closed = await _collections.ClosedPeriods
            .Find(p => p.Year == period.Year && p.Month == period.Month)
            .AnyAsync(token)
            .ConfigureAwait(false);

        TimeEntryRules.EnsurePeriodOpen(period, closed);
    }

    /// <summary>
    /// Сумма часов сотрудника за дату по всем проектам, за вычетом одной
    /// записи.
    /// </summary>
    /// <param name="excludeEntryId">
    /// Идентификатор редактируемой записи. Её прежние часы в сумму не входят,
    /// иначе правка «8 ч → 9 ч» отклонялась бы собственным старым значением
    /// (NOTES.md, п. 1.3).
    /// </param>
    public async Task<decimal> SumDayHoursAsync(
        string employeeId, DateOnly date, string? excludeEntryId, CancellationToken token)
    {
        var filter = Builders<TimeEntry>.Filter.Eq(e => e.EmployeeId, employeeId)
                     & Builders<TimeEntry>.Filter.Eq(e => e.Date, date);

        if (excludeEntryId is not null)
            filter &= Builders<TimeEntry>.Filter.Ne(e => e.Id, excludeEntryId);

        // Сумма считается в базе ($group), а не выгрузкой записей дня в память.
        var cursor = await _collections.TimeEntries.AggregateAsync<BsonDocument>(new[]
        {
            new BsonDocument("$match", filter.Render(
                _collections.TimeEntries.DocumentSerializer,
                _collections.TimeEntries.Settings.SerializerRegistry)),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total", new BsonDocument("$sum", new BsonDocument("$toDecimal", "$hours")) }
            })
        }, cancellationToken: token).ConfigureAwait(false);

        var document = await cursor.FirstOrDefaultAsync(token).ConfigureAwait(false);

        return BsonValues.ToDecimal(document, "total");
    }

    /// <summary>
    /// Полный прогон правил, общий для создания и изменения записи.
    /// Порядок проверок выбран так, чтобы пользователь сначала видел ошибку
    /// в том, что он только что ввёл, и лишь потом — конфликты с состоянием базы.
    /// </summary>
    public async Task ValidateAsync(
        string employeeId, string projectId, DateOnly date, decimal hours,
        string? excludeEntryId, CancellationToken token)
    {
        TimeEntryRules.EnsureHoursValid(hours);

        var employee = await RequireEmployeeAsync(employeeId, token).ConfigureAwait(false);
        var project = await RequireProjectAsync(projectId, token).ConfigureAwait(false);

        await EnsurePeriodOpenAsync(date, token).ConfigureAwait(false);

        TimeEntryRules.EnsureWithinProjectPeriod(project, date);
        TimeEntryRules.RequireRateAt(employee, date);

        var otherHours = await SumDayHoursAsync(employeeId, date, excludeEntryId, token).ConfigureAwait(false);
        TimeEntryRules.EnsureDailyLimit(otherHours, hours, date);
    }
}
