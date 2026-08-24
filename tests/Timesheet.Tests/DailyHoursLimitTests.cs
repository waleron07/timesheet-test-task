namespace Timesheet.Tests;

/// <summary>
/// Обязательный тест из ТЗ № 2: лимит часов за день.
/// Суммарно по всем проектам — не больше 24 часов за календарный день.
/// </summary>
public class DailyHoursLimitTests
{
    private static readonly DateOnly Дата = TestData.D(6, 3);

    [Fact]
    public void Ровно_24_часа_за_день_допустимы()
    {
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 16m, newHours: 8m, Дата);

        act.Should().NotThrow();
    }

    [Fact]
    public void Превышение_24_часов_отклоняется()
    {
        // Сценарии 2 и 3 приёмки: 20 ч уже есть, добавляем 6 ч → 26 ч, отказ.
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 20m, newHours: 6m, Дата);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.DailyLimitExceeded);
    }

    [Fact]
    public void Сообщение_об_ошибке_содержит_цифры_а_не_только_факт_отказа()
    {
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 20m, newHours: 6m, Дата);

        // Пользователь должен понять, сколько уже занято и на сколько он вышел
        // за лимит, — «внятный текст» из формулировки ТЗ.
        act.Should().Throw<BusinessRuleException>()
            .Which.Message.Should().Contain("26").And.Contain("20").And.Contain("06.03.2026");
    }

    [Fact]
    public void Лимит_считается_по_всем_проектам_вместе()
    {
        // 10 ч на одном проекте и 15 ч на другом — превышение, хотя по
        // отдельности каждая запись допустима.
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 10m, newHours: 15m, Дата);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Пересохранение_записи_без_изменений_не_упирается_в_лимит()
    {
        // День заполнен ровно на 24 ч: 16 ч чужих записей и 8 ч редактируемой.
        // Пересохранение той же записи с теми же 8 ч обязано пройти.
        // Наивная реализация, складывающая все записи дня вместе с прежним
        // значением редактируемой, получила бы 24 + 8 = 32 ч и отклонила
        // операцию, в которой ничего не изменилось.
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 16m, newHours: 8m, Дата);

        act.Should().NotThrow();
    }

    [Fact]
    public void Уменьшение_часов_в_заполненном_дне_разрешено()
    {
        // Тот же день на 24 ч, правка 8 ч → 7 ч: в сумму входят только чужие 16 ч.
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 16m, newHours: 7m, Дата);

        act.Should().NotThrow();
    }

    [Fact]
    public void Увеличение_часов_сверх_лимита_отклоняется_и_при_редактировании()
    {
        // Исключение прежнего значения не должно превращаться в дыру:
        // 16 ч чужих + 8,5 ч — это 24,5 ч, и отказ здесь правильный.
        var act = () => TimeEntryRules.EnsureDailyLimit(otherHoursThatDay: 16m, newHours: 8.5m, Дата);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.DailyLimitExceeded);
    }
}
