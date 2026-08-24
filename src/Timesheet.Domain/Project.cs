namespace Timesheet.Domain;

public sealed class Project
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Шифр проекта, уникальный (например, П-001).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal Budget { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Дата окончания. null — проект бессрочный.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Попадает ли дата в период проекта. Границы включительные:
    /// работать в первый и последний день проекта можно.
    /// </summary>
    public bool CoversDate(DateOnly date) =>
        date >= StartDate && (EndDate is null || date <= EndDate.Value);
}
