namespace Timesheet.Tests;

/// <summary>
/// Обязательный тест из ТЗ № 1: выбор ставки по дате.
/// Ставка действует с указанной даты до начала следующей.
/// </summary>
public class RateSelectionTests
{
    [Fact]
    public void Ставка_действует_с_указанной_даты_включительно()
    {
        var ivanov = TestData.Ivanov();

        ivanov.RateAt(TestData.D(1, 3))!.Value.Should().Be(600m);
    }

    [Fact]
    public void За_день_до_смены_действует_прежняя_ставка()
    {
        var ivanov = TestData.Ivanov();

        ivanov.RateAt(TestData.D(28, 2))!.Value.Should().Be(500m);
    }

    [Fact]
    public void После_смены_действует_новая_ставка_и_дальше()
    {
        var ivanov = TestData.Ivanov();

        ivanov.RateAt(TestData.D(31, 12))!.Value.Should().Be(600m);
    }

    [Fact]
    public void До_первой_ставки_ставки_нет()
    {
        // Петрова на 15.01.2026 — сценарий 1 из приёмочных проверок.
        var petrova = TestData.Petrova();

        petrova.RateAt(TestData.D(15, 1)).Should().BeNull();
    }

    [Fact]
    public void Порядок_ставок_в_списке_не_влияет_на_результат()
    {
        // Именно опора на порядок элементов (Rates.FirstOrDefault) была
        // ошибкой в коде из части 1: она давала произвольную ставку.
        var прямой = TestData.Ivanov();
        var обратный = TestData.Ivanov();
        обратный.Rates.Reverse();

        обратный.RateAt(TestData.D(5, 3))!.Value
            .Should().Be(прямой.RateAt(TestData.D(5, 3))!.Value)
            .And.Be(600m);
    }

    [Fact]
    public void Ставка_добавленная_задним_числом_меняет_результат()
    {
        // Сценарий 8 из приёмочных проверок: ставку Иванова с 01.03.2026
        // поменяли на 650 ₽ — стоимость записи от 05.03.2026 обязана измениться.
        var ivanov = TestData.Ivanov();
        ivanov.Rates.Single(r => r.From == TestData.D(1, 3)).Value = 650m;

        var rate = TimeEntryRules.RequireRateAt(ivanov, TestData.D(5, 3));

        TimeEntryRules.CalculateAmount(8m, rate.Value).Should().Be(5_200m);
    }

    [Fact]
    public void Отсутствие_ставки_на_дату_запрещает_создание_записи()
    {
        var petrova = TestData.Petrova();

        var act = () => TimeEntryRules.RequireRateAt(petrova, TestData.D(15, 1));

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be(ErrorCodes.RateNotFound);
    }
}
