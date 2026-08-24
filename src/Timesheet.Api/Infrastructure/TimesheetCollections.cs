using MongoDB.Driver;
using Timesheet.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Типизированный доступ к коллекциям. Обработчики получают его через DI и
/// не собирают GetCollection&lt;T&gt;("строка") у себя внутри.
/// </summary>
public sealed class TimesheetCollections
{
    public TimesheetCollections(IMongoDatabase database)
    {
        Database = database;
        TimeEntries = database.GetCollection<TimeEntry>(CollectionNames.TimeEntries);
        Employees = database.GetCollection<Employee>(CollectionNames.Employees);
        Projects = database.GetCollection<Project>(CollectionNames.Projects);
        ClosedPeriods = database.GetCollection<ClosedPeriod>(CollectionNames.ClosedPeriods);
    }

    public IMongoDatabase Database { get; }
    public IMongoCollection<TimeEntry> TimeEntries { get; }
    public IMongoCollection<Employee> Employees { get; }
    public IMongoCollection<Project> Projects { get; }
    public IMongoCollection<ClosedPeriod> ClosedPeriods { get; }
}
