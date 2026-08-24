namespace Timesheet.Domain;

/// <summary>
/// Единственная точка округления денег в системе.
/// </summary>
public static class Money
{
    /// <summary>
    /// Округление до копеек.
    ///
    /// MidpointRounding.AwayFromZero, а не поведение decimal.Round по умолчанию
    /// (ToEven, «банковское»): по умолчанию 2,345 превратилось бы в 2,34, что
    /// расходится с ожиданием бухгалтера и с тем, как считает калькулятор.
    /// Правило должно быть одно на всю систему, поэтому Math.Round по месту
    /// вызова в коде запрещён — только этот метод.
    /// </summary>
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
