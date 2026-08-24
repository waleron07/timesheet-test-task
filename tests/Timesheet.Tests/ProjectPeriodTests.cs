namespace Timesheet.Tests;

/// <summary>
/// Обязательный тест из ТЗ № 4: границы периода проекта.
/// Дата записи не раньше даты начала и не позже даты окончания, если она задана.
/// </summary>
public class ProjectPeriodTests
{
    [Fact]
    public void Первый_день_проекта_допустим()
    {
        TestData.P001().CoversDate(TestData.D(1, 1)).Should().BeTrue();
    }

    [Fact]
    public void Последний_день_проекта_допустим()
    {
        TestData.P001().CoversDate(TestData.D(31, 3)).Should().BeTrue();
    }

    [Fact]
    public void Дата_раньше_начала_отклоняется()
    {
        // Сценарий 4 приёмки: запись на П-002 датой 20.02.2026 —
        // раньше начала проекта (01.03.2026).
        var act = () => TimeEntryRules.EnsureWithinProjectPeriod(TestData.P002(), TestData.D(20, 2));

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.DateOutOfProjectRange);
    }

    [Fact]
    public void Дата_позже_окончания_отклоняется()
    {
        var act = () => TimeEntryRules.EnsureWithinProjectPeriod(TestData.P001(), TestData.D(1, 4));

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.DateOutOfProjectRange);
    }

    [Fact]
    public void Бессрочный_проект_не_имеет_верхней_границы()
    {
        var p002 = TestData.P002();

        p002.CoversDate(TestData.D(1, 3)).Should().BeTrue();
        p002.CoversDate(TestData.D(31, 12, 2030)).Should().BeTrue();
    }

    [Fact]
    public void Сообщение_называет_шифр_проекта_и_его_период()
    {
        var act = () => TimeEntryRules.EnsureWithinProjectPeriod(TestData.P002(), TestData.D(20, 2));

        act.Should().Throw<BusinessRuleException>()
            .Which.Message.Should().Contain("П-002").And.Contain("01.03.2026");
    }
}
