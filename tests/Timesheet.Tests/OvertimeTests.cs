namespace Timesheet.Tests;

/// <summary>
/// Если за день у сотрудника получилось больше 12 часов, запись сохраняется,
/// но день помечается как переработка.
/// </summary>
public class OvertimeTests
{
    [Fact]
    public void Ровно_12_часов_переработкой_не_считаются()
    {
        // ТЗ: «больше 12 часов», то есть 12 — ещё норма.
        TimeEntryRules.IsOvertime(12m).Should().BeFalse();
    }

    [Fact]
    public void Больше_12_часов_помечается_как_переработка()
    {
        TimeEntryRules.IsOvertime(12.5m).Should().BeTrue();

        // Сценарий 2 приёмки: 20 часов сохраняются, но день — переработка.
        TimeEntryRules.IsOvertime(20m).Should().BeTrue();
    }

    [Fact]
    public void Переработка_не_мешает_сохранению_записи()
    {
        // 20 часов одной записью: допустимы и по лимиту записи, и по лимиту дня.
        var часы = () => TimeEntryRules.EnsureHoursValid(20m);
        var лимитДня = () => TimeEntryRules.EnsureDailyLimit(0m, 20m, TestData.D(6, 3));

        часы.Should().NotThrow();
        лимитДня.Should().NotThrow();
        TimeEntryRules.IsOvertime(20m).Should().BeTrue();
    }

    [Fact]
    public void Переработка_считается_по_сумме_дня_а_не_по_одной_записи()
    {
        // Две записи по 7 часов на разных проектах: каждая сама по себе
        // не переработка, а день — да.
        TimeEntryRules.IsOvertime(7m).Should().BeFalse();
        TimeEntryRules.IsOvertime(7m + 7m).Should().BeTrue();
    }
}
