namespace Timesheet.Domain;

/// <summary>Год и месяц как единое значение — ключ закрытого периода.</summary>
public readonly record struct YearMonth(int Year, int Month)
{
    public static YearMonth Of(DateOnly date) => new(date.Year, date.Month);

    public override string ToString() => $"{Month:D2}.{Year}";
}

/// <summary>Месяц, закрытый бухгалтерией для редактирования.</summary>
public sealed class ClosedPeriod
{
    public string Id { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    public YearMonth Period => new(Year, Month);
}
