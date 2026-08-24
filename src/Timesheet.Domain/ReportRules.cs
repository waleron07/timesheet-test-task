namespace Timesheet.Domain;

/// <summary>
/// Правила отчёта по проектам. Пороги вынесены сюда, чтобы значения 80 и 100
/// не оказались зашитыми одновременно в обработчик отчёта и во фронт.
/// </summary>
public static class ReportRules
{
    /// <summary>Признак риска: освоено больше 80 % бюджета.</summary>
    public const decimal RiskThresholdPercent = 80m;

    /// <summary>Признак перерасхода: освоено больше 100 % бюджета.</summary>
    public const decimal OverspentThresholdPercent = 100m;

    /// <summary>
    /// Процент освоения бюджета.
    ///
    /// null при нулевом бюджете: процент от нулевой базы не определён.
    /// Возвращать 0 было бы враньём (деньги-то потрачены), а делить на ноль в
    /// decimal — это DivideByZeroException. В исходном коде из части 1 здесь
    /// был double, и получалась Infinity, ломающая сериализацию JSON.
    /// </summary>
    public static decimal? BudgetPercent(decimal amount, decimal budget) =>
        budget > 0m ? Money.Round(amount / budget * 100m) : null;

    public static bool IsOverspent(decimal? percent) => percent > OverspentThresholdPercent;

    public static bool IsAtRisk(decimal? percent) => percent > RiskThresholdPercent;
}
