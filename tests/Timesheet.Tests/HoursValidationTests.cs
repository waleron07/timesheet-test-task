namespace Timesheet.Tests;

/// <summary>
/// Часы: положительные, кратные 0,5, не больше 24 за одну запись.
/// Сценарий 6 приёмки: часы 0 или 3,7 — ошибка валидации.
/// </summary>
public class HoursValidationTests
{
    public static TheoryData<decimal> Допустимые => new() { 0.5m, 1m, 7.5m, 23.5m, 24m };
    public static TheoryData<decimal> Недопустимые => new() { 0m, -1m, 3.7m, 0.25m, 24.5m, 25m };

    [Theory]
    [MemberData(nameof(Допустимые))]
    public void Допустимые_значения_проходят(decimal hours)
    {
        var act = () => TimeEntryRules.EnsureHoursValid(hours);

        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(Недопустимые))]
    public void Недопустимые_значения_отклоняются(decimal hours)
    {
        var act = () => TimeEntryRules.EnsureHoursValid(hours);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.InvalidHours);
    }

    [Fact]
    public void Причина_отказа_различается_в_тексте()
    {
        // Пользователю должно быть понятно, что именно не так: ноль часов и
        // некратность 0,5 — разные ошибки, хоть и с одним кодом.
        var ноль = () => TimeEntryRules.EnsureHoursValid(0m);
        var некратно = () => TimeEntryRules.EnsureHoursValid(3.7m);

        ноль.Should().Throw<BusinessRuleException>()
            .Which.Message.Should().Contain("больше нуля");
        некратно.Should().Throw<BusinessRuleException>()
            .Which.Message.Should().Contain("0,5");
    }
}
