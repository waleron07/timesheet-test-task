namespace Timesheet.Domain;

/// <summary>Часовая ставка, действующая с указанной даты.</summary>
public sealed class Rate
{
    public DateOnly From { get; set; }
    public decimal Value { get; set; }
}

public sealed class Employee
{
    public string Id { get; set; } = string.Empty;

    /// <summary>ФИО.</summary>
    public string FullName { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// История ставок. Порядок элементов не гарантирован и не имеет значения:
    /// действующая ставка определяется датой, а не позицией в массиве.
    /// Именно опора на порядок была ошибкой в исходном коде из части 1.
    /// </summary>
    public List<Rate> Rates { get; set; } = new();

    /// <summary>
    /// Ставка, действовавшая на указанную дату: из всех ставок с From &lt;= date
    /// берётся с максимальной From. Ставка действует с указанной даты до начала
    /// следующей, поэтому «последняя назначенная к этому моменту» — и есть
    /// действующая.
    ///
    /// Возвращает null, если на дату не было ни одной ставки: это штатная
    /// ситуация (сотрудник ещё не оформлен), а не сбой. Решение, что с ней
    /// делать, принимает вызывающий код.
    /// </summary>
    public Rate? RateAt(DateOnly date)
    {
        Rate? result = null;

        foreach (var rate in Rates)
        {
            if (rate.From > date) continue;
            if (result is null || rate.From > result.From) result = rate;
        }

        return result;
    }
}
