namespace Timesheet.Tests;

/// <summary>
/// Обязательный тест из ТЗ № 5: округление денег.
/// Деньги — decimal, округление до копеек.
/// </summary>
public class MoneyRoundingTests
{
    [Fact]
    public void Округление_до_двух_знаков()
    {
        Money.Round(1234.5678m).Should().Be(1234.57m);
        Money.Round(1234.5612m).Should().Be(1234.56m);
    }

    [Fact]
    public void Половина_копейки_округляется_от_нуля_а_не_к_чётному()
    {
        // Поведение decimal.Round по умолчанию — ToEven: 2,345 → 2,34.
        // Бухгалтер ожидает 2,35, поэтому используется AwayFromZero.
        Money.Round(2.345m).Should().Be(2.35m);
        Money.Round(2.335m).Should().Be(2.34m);
    }

    [Fact]
    public void Стоимость_записи_считается_как_часы_умножить_на_ставку()
    {
        // Все четыре записи из приёмочных данных.
        TimeEntryRules.CalculateAmount(8m, 500m).Should().Be(4_000m);  // 20.02, Иванов, ставка 500
        TimeEntryRules.CalculateAmount(8m, 600m).Should().Be(4_800m);  // 05.03, Иванов, ставка уже 600
        TimeEntryRules.CalculateAmount(4m, 700m).Should().Be(2_800m);  // 05.03, Петрова
        TimeEntryRules.CalculateAmount(10m, 700m).Should().Be(7_000m); // 06.03, Петрова
    }

    [Fact]
    public void Дробные_часы_и_ставка_дают_копейки()
    {
        TimeEntryRules.CalculateAmount(7.5m, 333.33m).Should().Be(2_499.98m);
    }

    [Fact]
    public void Сумма_округлённых_записей_не_накапливает_погрешность()
    {
        // На double сложение 0,1 тысячу раз даёт 99,9999999999986 вместо 100.
        // На decimal результат точен — ровно за это ТЗ и запрещает double.
        var сумма = 0m;
        for (var i = 0; i < 1000; i++) сумма += TimeEntryRules.CalculateAmount(0.5m, 0.2m);

        сумма.Should().Be(100m);
    }
}
