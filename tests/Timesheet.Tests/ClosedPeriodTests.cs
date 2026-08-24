namespace Timesheet.Tests;

/// <summary>
/// Обязательный тест из ТЗ № 3: закрытый период.
/// В закрытом периоде записи нельзя создавать, изменять и удалять.
/// </summary>
public class ClosedPeriodTests
{
    [Fact]
    public void В_открытом_периоде_операции_разрешены()
    {
        var act = () => TimeEntryRules.EnsurePeriodOpen(new YearMonth(2026, 3), isClosed: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void В_закрытом_периоде_операции_запрещены()
    {
        // Сценарий 5 приёмки: закрыть февраль 2026 и попробовать изменить
        // запись от 20.02.2026 — отказ.
        var act = () => TimeEntryRules.EnsurePeriodOpen(new YearMonth(2026, 2), isClosed: true);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.PeriodClosed);
    }

    [Fact]
    public void Сообщение_называет_конкретный_месяц()
    {
        var act = () => TimeEntryRules.EnsurePeriodOpen(new YearMonth(2026, 2), isClosed: true);

        act.Should().Throw<BusinessRuleException>()
            .Which.Message.Should().Contain("02.2026");
    }

    [Fact]
    public void Период_записи_определяется_её_датой()
    {
        YearMonth.Of(TestData.D(20, 2)).Should().Be(new YearMonth(2026, 2));
        YearMonth.Of(TestData.D(1, 3)).Should().Be(new YearMonth(2026, 3));
        YearMonth.Of(TestData.D(31, 3)).Should().Be(new YearMonth(2026, 3));
    }

    [Fact]
    public void Перенос_записи_проверяет_оба_периода()
    {
        // Допущение 1.4 из NOTES.md: запись переносят с 20.02 (февраль закрыт)
        // на 05.03 (март открыт). Проверка только нового периода пропустила бы
        // операцию, и часы «утекли» бы из закрытого месяца.
        var исходный = YearMonth.Of(TestData.D(20, 2));
        var новый = YearMonth.Of(TestData.D(5, 3));

        var actНовый = () => TimeEntryRules.EnsurePeriodOpen(новый, isClosed: false);
        var actИсходный = () => TimeEntryRules.EnsurePeriodOpen(исходный, isClosed: true);

        actНовый.Should().NotThrow();
        actИсходный.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.PeriodClosed);
    }
}
