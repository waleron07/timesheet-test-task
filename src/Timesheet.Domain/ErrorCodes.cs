namespace Timesheet.Domain;

/// <summary>
/// Машиночитаемые коды ошибок бизнес-правил. Уезжают клиенту в теле ответа,
/// чтобы фронт мог различать ситуации не по тексту сообщения.
/// Сопоставление кода с HTTP-статусом — забота слоя API, домен про HTTP не знает.
/// </summary>
public static class ErrorCodes
{
    public const string InvalidHours = "INVALID_HOURS";
    public const string RateNotFound = "RATE_NOT_FOUND";
    public const string DateOutOfProjectRange = "DATE_OUT_OF_PROJECT_RANGE";
    public const string DailyLimitExceeded = "DAILY_LIMIT_EXCEEDED";
    public const string PeriodClosed = "PERIOD_CLOSED";
    public const string ConcurrentModification = "CONCURRENT_MODIFICATION";
    public const string EmployeeNotFound = "EMPLOYEE_NOT_FOUND";
    public const string ProjectNotFound = "PROJECT_NOT_FOUND";
    public const string TimeEntryNotFound = "TIME_ENTRY_NOT_FOUND";
}

/// <summary>
/// Нарушение бизнес-правила: операция корректна по форме, но недопустима
/// в текущем состоянии данных. Отличается от ошибки валидации входа тем,
/// что для её проверки нужно заглянуть в базу.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>Код из <see cref="ErrorCodes"/>.</summary>
    public string Code { get; }
}
