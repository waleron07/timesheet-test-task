namespace Timesheet.Tests;

/// <summary>
/// Цифры из раздела «Приёмочные проверки», посчитанные доменом.
///
/// Этот класс — эталон для Mongo-агрегации: отчёт по ТЗ обязан считаться в базе,
/// то есть правило выбора ставки существует в двух реализациях (C# и пайплайн).
/// Здесь зафиксировано, что должно получиться; интеграционный тест на этапе 3
/// сверит с этим результат пайплайна.
/// </summary>
public class AcceptanceFiguresTests
{
    private sealed record Запись(Employee Сотрудник, Project Проект, DateOnly Дата, decimal Часы);

    private static readonly Employee Иванов = TestData.Ivanov();
    private static readonly Employee Петрова = TestData.Petrova();
    private static readonly Project П001 = TestData.P001();
    private static readonly Project П002 = TestData.P002();

    /// <summary>Четыре записи табеля из приёмочных данных.</summary>
    private static readonly Запись[] Табель =
    [
        new(Иванов, П001, TestData.D(20, 2), 8m),
        new(Иванов, П001, TestData.D(5, 3), 8m),
        new(Петрова, П001, TestData.D(5, 3), 4m),
        new(Петрова, П002, TestData.D(6, 3), 10m)
    ];

    private static decimal Стоимость(Запись з)
    {
        var ставка = TimeEntryRules.RequireRateAt(з.Сотрудник, з.Дата);
        return TimeEntryRules.CalculateAmount(з.Часы, ставка.Value);
    }

    private static (decimal Часы, decimal Стоимость) ИтогПоПроекту(Project проект, int год, int месяц) =>
        Табель
            .Where(з => з.Проект.Id == проект.Id && з.Дата.Year == год && з.Дата.Month == месяц)
            .Aggregate((Часы: 0m, Стоимость: 0m),
                (итог, з) => (итог.Часы + з.Часы, итог.Стоимость + Стоимость(з)));

    [Fact]
    public void Стоимость_каждой_записи_совпадает_с_ожидаемой()
    {
        Стоимость(Табель[0]).Should().Be(4_000m);
        Стоимость(Табель[1]).Should().Be(4_800m); // ставка уже 600
        Стоимость(Табель[2]).Should().Be(2_800m);
        Стоимость(Табель[3]).Should().Be(7_000m);
    }

    [Fact]
    public void Отчёт_за_март_2026_по_проекту_П001()
    {
        var (часы, стоимость) = ИтогПоПроекту(П001, 2026, 3);

        часы.Should().Be(12m);
        стоимость.Should().Be(7_600m);
        ReportRules.BudgetPercent(стоимость, П001.Budget).Should().Be(38m);
        ReportRules.IsOverspent(38m).Should().BeFalse();
        ReportRules.IsAtRisk(38m).Should().BeFalse();
    }

    [Fact]
    public void Отчёт_за_март_2026_по_проекту_П002_показывает_перерасход()
    {
        var (часы, стоимость) = ИтогПоПроекту(П002, 2026, 3);

        часы.Should().Be(10m);
        стоимость.Should().Be(7_000m);

        var процент = ReportRules.BudgetPercent(стоимость, П002.Budget);
        процент.Should().Be(140m);
        ReportRules.IsOverspent(процент).Should().BeTrue();
        ReportRules.IsAtRisk(процент).Should().BeTrue();
    }

    [Fact]
    public void Итоговая_строка_за_март_2026()
    {
        var март = Табель.Where(з => з.Дата is { Year: 2026, Month: 3 }).ToList();

        март.Sum(з => з.Часы).Should().Be(22m);
        март.Sum(Стоимость).Should().Be(14_600m);
    }

    [Fact]
    public void Отчёт_за_февраль_2026()
    {
        var (часы, стоимость) = ИтогПоПроекту(П001, 2026, 2);

        часы.Should().Be(8m);
        стоимость.Should().Be(4_000m);
        ReportRules.BudgetPercent(стоимость, П001.Budget).Should().Be(20m);
    }

    [Fact]
    public void Правка_ставки_задним_числом_меняет_отчёт_за_март()
    {
        // Сценарий 8: ставка Иванова с 01.03.2026 меняется на 650 ₽.
        // Стоимость записи от 05.03 становится 5 200 ₽, а итог П-001 за март —
        // 8 000 ₽ вместо 7 600 ₽. Это и есть причина, по которой стоимость
        // не денормализуется в запись (см. NOTES.md, п. 1.1).
        var иванов = TestData.Ivanov();
        иванов.Rates.Single(r => r.From == TestData.D(1, 3)).Value = 650m;

        var ставка = TimeEntryRules.RequireRateAt(иванов, TestData.D(5, 3));
        var новаяСтоимость = TimeEntryRules.CalculateAmount(8m, ставка.Value);

        новаяСтоимость.Should().Be(5_200m);
        (новаяСтоимость + 2_800m).Should().Be(8_000m);
    }

    [Fact]
    public void Процент_освоения_не_определён_при_нулевом_бюджете()
    {
        ReportRules.BudgetPercent(1_000m, 0m).Should().BeNull();
        ReportRules.IsOverspent(null).Should().BeFalse();
    }
}
