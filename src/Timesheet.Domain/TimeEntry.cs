namespace Timesheet.Domain;

public sealed class TimeEntry
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string? Comment { get; set; }

    /// <summary>
    /// Версия записи для оптимистичной блокировки. Инкрементируется при каждом
    /// изменении; апдейт выполняется с фильтром по текущей версии, поэтому
    /// параллельная правка не затирается молча.
    /// </summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
