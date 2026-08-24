using MongoDB.Driver;
using Timesheet.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Наполнение базы приёмочными данными из задания.
///
/// Команда сносящая: коллекции очищаются и заливаются заново. Это осознанно.
/// Сценарии с ошибками из задания добавляют в базу лишние записи (в частности,
/// 20 часов Иванову на 06.03.2026 сохраняются успешно — так и задумано), после
/// чего отчёт за март показывает 32 часа вместо ожидаемых 12 по П-001.
/// Возможность привести базу в эталонное состояние одной командой важнее
/// сохранности данных в учебном проекте.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly TimesheetCollections _collections;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(TimesheetCollections collections, ILogger<DatabaseSeeder> logger)
    {
        _collections = collections;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken token = default)
    {
        _logger.LogInformation("Очистка коллекций перед наполнением");

        await _collections.TimeEntries.DeleteManyAsync(FilterDefinition<TimeEntry>.Empty, token);
        await _collections.Employees.DeleteManyAsync(FilterDefinition<Employee>.Empty, token);
        await _collections.Projects.DeleteManyAsync(FilterDefinition<Project>.Empty, token);
        await _collections.ClosedPeriods.DeleteManyAsync(FilterDefinition<ClosedPeriod>.Empty, token);

        await _collections.Employees.InsertManyAsync(Employees(), cancellationToken: token);
        await _collections.Projects.InsertManyAsync(Projects(), cancellationToken: token);
        await _collections.TimeEntries.InsertManyAsync(TimeEntries(), cancellationToken: token);

        _logger.LogInformation(
            "База наполнена: {Employees} сотрудника, {Projects} проекта, {Entries} записи табеля",
            2, 2, 4);
    }

    private static DateOnly D(int day, int month) => new(2026, month, day);

    private static List<Employee> Employees() =>
    [
        new()
        {
            Id = EmployeeIds.Ivanov,
            FullName = "Иванов И. И.",
            Department = "Проектный",
            Rates =
            [
                new Rate { From = D(1, 1), Value = 500m },
                new Rate { From = D(1, 3), Value = 600m }
            ]
        },
        new()
        {
            Id = EmployeeIds.Petrova,
            FullName = "Петрова А. С.",
            Department = "Проектный",
            Rates =
            [
                new Rate { From = D(1, 2), Value = 700m }
            ]
        }
    ];

    private static List<Project> Projects() =>
    [
        new()
        {
            Id = ProjectIds.P001,
            Code = "П-001",
            Name = "Реконструкция цеха",
            Budget = 20_000m,
            StartDate = D(1, 1),
            EndDate = D(31, 3)
        },
        new()
        {
            Id = ProjectIds.P002,
            Code = "П-002",
            Name = "Инженерные сети",
            Budget = 5_000m,
            StartDate = D(1, 3),
            EndDate = null
        }
    ];

    /// <summary>
    /// Ровно четыре записи из таблицы «Записи табеля». Записи из раздела
    /// «Сценарии с ошибками» сюда не входят: это сценарии ручной проверки,
    /// а не эталонные данные.
    /// </summary>
    private static List<TimeEntry> TimeEntries()
    {
        var now = DateTime.UtcNow;

        return
        [
            Entry("te-001", EmployeeIds.Ivanov, ProjectIds.P001, D(20, 2), 8m, "Демонтаж перекрытий", now),
            Entry("te-002", EmployeeIds.Ivanov, ProjectIds.P001, D(5, 3), 8m, "Монтаж оборудования", now),
            Entry("te-003", EmployeeIds.Petrova, ProjectIds.P001, D(5, 3), 4m, "Рабочая документация", now),
            Entry("te-004", EmployeeIds.Petrova, ProjectIds.P002, D(6, 3), 10m, "Трассировка сетей", now)
        ];
    }

    private static TimeEntry Entry(
        string id, string employeeId, string projectId, DateOnly date, decimal hours, string comment, DateTime now) =>
        new()
        {
            Id = id,
            EmployeeId = employeeId,
            ProjectId = projectId,
            Date = date,
            Hours = hours,
            Comment = comment,
            Version = 1,
            CreatedAt = now,
            CreatedBy = "seed"
        };

    private static class EmployeeIds
    {
        public const string Ivanov = "emp-ivanov";
        public const string Petrova = "emp-petrova";
    }

    private static class ProjectIds
    {
        public const string P001 = "prj-001";
        public const string P002 = "prj-002";
    }
}
