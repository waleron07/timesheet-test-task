namespace Timesheet.Tests;

/// <summary>
/// Сотрудники и проекты из раздела «Приёмочные проверки» задания.
/// Тесты считают на тех же данных, по которым результат будут проверять.
/// </summary>
public static class TestData
{
    public static DateOnly D(int day, int month, int year = 2026) => new(year, month, day);

    /// <summary>Иванов И. И.: 500 ₽/ч с 01.01.2026, 600 ₽/ч с 01.03.2026.</summary>
    public static Employee Ivanov() => new()
    {
        Id = "emp-ivanov",
        FullName = "Иванов И. И.",
        Department = "Проектный",
        Rates =
        {
            new Rate { From = D(1, 1), Value = 500m },
            new Rate { From = D(1, 3), Value = 600m }
        }
    };

    /// <summary>Петрова А. С.: 700 ₽/ч с 01.02.2026.</summary>
    public static Employee Petrova() => new()
    {
        Id = "emp-petrova",
        FullName = "Петрова А. С.",
        Department = "Проектный",
        Rates =
        {
            new Rate { From = D(1, 2), Value = 700m }
        }
    };

    /// <summary>П-001: бюджет 20 000 ₽, 01.01.2026 – 31.03.2026.</summary>
    public static Project P001() => new()
    {
        Id = "prj-001",
        Code = "П-001",
        Name = "Реконструкция цеха",
        Budget = 20_000m,
        StartDate = D(1, 1),
        EndDate = D(31, 3)
    };

    /// <summary>П-002: бюджет 5 000 ₽, с 01.03.2026, бессрочный.</summary>
    public static Project P002() => new()
    {
        Id = "prj-002",
        Code = "П-002",
        Name = "Инженерные сети",
        Budget = 5_000m,
        StartDate = D(1, 3),
        EndDate = null
    };
}
