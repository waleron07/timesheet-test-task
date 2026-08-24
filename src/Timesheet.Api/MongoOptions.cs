namespace Timesheet.Api;

/// <summary>Настройки подключения к MongoDB (секция "Mongo" в конфигурации).</summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "timesheet";
}
