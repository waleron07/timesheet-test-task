using MongoDB.Driver;
using Timesheet.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Явное создание индексов при старте приложения.
///
/// Операция идемпотентна: CreateMany с теми же ключами и именем ничего не
/// делает, если индекс уже есть. Полагаться на то, что индексы «кто-то создал
/// руками на проде», нельзя — их отсутствие проявляется не ошибкой, а
/// медленным отчётом под нагрузкой.
///
/// Обоснование каждого индекса — в NOTES.md, раздел 3.
/// </summary>
public static class MongoIndexes
{
    public static async Task EnsureAsync(TimesheetCollections collections, CancellationToken token = default)
    {
        // Основной индекс табеля. Порядок полей не случаен: date идёт первым,
        // потому что по нему всегда идёт диапазонный предикат (месяц), а
        // фильтры по сотруднику и проекту опциональны — на них нельзя
        // опереться как на префикс составного индекса.
        var entryKeys = Builders<TimeEntry>.IndexKeys
            .Ascending(e => e.Date)
            .Ascending(e => e.EmployeeId)
            .Ascending(e => e.ProjectId);

        // Под проверку суточного лимита и признак переработки: все записи
        // одного сотрудника за конкретную дату. Здесь оба предиката —
        // равенства, поэтому первым идёт более селективное поле.
        var dailyKeys = Builders<TimeEntry>.IndexKeys
            .Ascending(e => e.EmployeeId)
            .Ascending(e => e.Date);

        await collections.TimeEntries.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeEntry>(entryKeys, new CreateIndexOptions { Name = "ix_date_employee_project" }),
            new CreateIndexModel<TimeEntry>(dailyKeys, new CreateIndexOptions { Name = "ix_employee_date" })
        }, token);

        // Шифр проекта уникален по ТЗ. Уникальность обеспечивает база, а не
        // только проверка в коде: без неё гонка двух параллельных вставок
        // спокойно создаёт дубль.
        await collections.Projects.Indexes.CreateOneAsync(
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys.Ascending(p => p.Code),
                new CreateIndexOptions { Name = "ux_code", Unique = true }),
            cancellationToken: token);

        // Закрытый период ищется при каждой операции над записью — это самый
        // частый запрос в системе. Уникальность запрещает два документа на
        // один и тот же месяц.
        await collections.ClosedPeriods.Indexes.CreateOneAsync(
            new CreateIndexModel<ClosedPeriod>(
                Builders<ClosedPeriod>.IndexKeys.Ascending(p => p.Year).Ascending(p => p.Month),
                new CreateIndexOptions { Name = "ux_year_month", Unique = true }),
            cancellationToken: token);
    }
}
