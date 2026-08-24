namespace Timesheet.Domain;

/// <summary>
/// Бизнес-правила записи табеля — чистые функции без обращений к базе.
///
/// Правила намеренно вынесены из обработчиков команд: они нужны при создании,
/// при редактировании и (в части выбора ставки) в отчёте. Держать их внутри
/// одного обработчика — прямой путь к тому, что второй потребитель напишет
/// свою слегка другую версию; ровно это и произошло в коде из части 1.
///
/// Каждый метод либо ничего не делает, либо бросает BusinessRuleException
/// с кодом и текстом на русском.
/// </summary>
public static class TimeEntryRules
{
    /// <summary>Максимум часов в одной записи.</summary>
    public const decimal MaxHoursPerEntry = 24m;

    /// <summary>Максимум часов у сотрудника за календарный день по всем проектам.</summary>
    public const decimal MaxHoursPerDay = 24m;

    /// <summary>Свыше этого числа часов за день день помечается как переработка.</summary>
    public const decimal OvertimeThresholdPerDay = 12m;

    /// <summary>Часы задаются с шагом в полчаса.</summary>
    public const decimal HoursStep = 0.5m;

    /// <summary>
    /// Часы: положительные, кратные 0,5, не больше 24 за одну запись.
    /// Границы включительные — ровно 24 часа одной записью допустимы.
    /// </summary>
    public static void EnsureHoursValid(decimal hours)
    {
        if (hours <= 0m)
            throw new BusinessRuleException(ErrorCodes.InvalidHours,
                "Количество часов должно быть больше нуля.");

        if (hours > MaxHoursPerEntry)
            throw new BusinessRuleException(ErrorCodes.InvalidHours,
                $"Количество часов в одной записи не может превышать {MaxHoursPerEntry:0.##}.");

        // Остаток от деления decimal вычисляется точно, без погрешности
        // двоичной плавающей точки — на double такая проверка была бы ненадёжной.
        if (hours % HoursStep != 0m)
            throw new BusinessRuleException(ErrorCodes.InvalidHours,
                "Количество часов должно быть кратно 0,5.");
    }

    /// <summary>
    /// Ставка, действовавшая на дату записи. Если на эту дату у сотрудника нет
    /// ни одной ставки — запись создать нельзя (правило 1 из ТЗ).
    /// </summary>
    public static Rate RequireRateAt(Employee employee, DateOnly date)
    {
        var rate = employee.RateAt(date);

        if (rate is null)
            throw new BusinessRuleException(ErrorCodes.RateNotFound,
                $"У сотрудника «{employee.FullName}» нет часовой ставки, действующей на {date:dd.MM.yyyy}. " +
                "Задайте ставку с этой или более ранней даты.");

        return rate;
    }

    /// <summary>Дата записи должна попадать в период проекта.</summary>
    public static void EnsureWithinProjectPeriod(Project project, DateOnly date)
    {
        if (project.CoversDate(date)) return;

        var period = project.EndDate is null
            ? $"с {project.StartDate:dd.MM.yyyy}, без даты окончания"
            : $"с {project.StartDate:dd.MM.yyyy} по {project.EndDate.Value:dd.MM.yyyy}";

        throw new BusinessRuleException(ErrorCodes.DateOutOfProjectRange,
            $"Дата {date:dd.MM.yyyy} не входит в период проекта {project.Code} ({period}).");
    }

    /// <summary>
    /// Суммарно за календарный день по всем проектам — не больше 24 часов.
    /// </summary>
    /// <param name="otherHoursThatDay">
    /// Часы сотрудника за эту дату по всем остальным записям. При редактировании
    /// прежние часы самой изменяемой записи сюда не входят — иначе правка
    /// «8 ч → 9 ч» отклонялась бы собственным старым значением.
    /// </param>
    public static void EnsureDailyLimit(decimal otherHoursThatDay, decimal newHours, DateOnly date)
    {
        var total = otherHoursThatDay + newHours;

        if (total <= MaxHoursPerDay) return;

        throw new BusinessRuleException(ErrorCodes.DailyLimitExceeded,
            $"За {date:dd.MM.yyyy} у сотрудника получится {total:0.##} ч при максимуме {MaxHoursPerDay:0.##} ч. " +
            $"Уже учтено {otherHoursThatDay:0.##} ч, добавляется {newHours:0.##} ч.");
    }

    /// <summary>
    /// В закрытом периоде записи нельзя создавать, изменять и удалять.
    /// </summary>
    public static void EnsurePeriodOpen(YearMonth period, bool isClosed)
    {
        if (!isClosed) return;

        throw new BusinessRuleException(ErrorCodes.PeriodClosed,
            $"Период {period} закрыт бухгалтерией: записи за этот месяц нельзя создавать, изменять и удалять.");
    }

    /// <summary>
    /// Признак переработки дня: больше 12 часов суммарно за календарный день.
    ///
    /// Свойство дня, а не отдельной записи: какая именно запись «перевалила
    /// порог», зависит от порядка ввода, а сумма за день — нет.
    /// </summary>
    public static bool IsOvertime(decimal totalHoursThatDay) => totalHoursThatDay > OvertimeThresholdPerDay;

    /// <summary>Стоимость записи: часы × ставка, округление до копеек.</summary>
    public static decimal CalculateAmount(decimal hours, decimal rate) => Money.Round(hours * rate);
}
