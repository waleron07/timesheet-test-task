using MongoDB.Bson;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Чтение значений из результата агрегации. Отдельный хелпер, потому что
/// каждое поле здесь может отсутствовать или быть null: $lookup с
/// preserveNullAndEmptyArrays именно для того и нужен, чтобы одна битая
/// ссылка не роняла весь запрос.
/// </summary>
public static class BsonValues
{
    public static decimal ToDecimal(BsonDocument? doc, string path)
    {
        var value = Resolve(doc, path);
        return value is null || value.IsBsonNull ? 0m : value.ToDecimal();
    }

    public static decimal? ToNullableDecimal(BsonDocument? doc, string path)
    {
        var value = Resolve(doc, path);
        return value is null || value.IsBsonNull ? null : value.ToDecimal();
    }

    public static string? ToNullableString(BsonDocument? doc, string path)
    {
        var value = Resolve(doc, path);
        return value is null || value.IsBsonNull ? null : value.AsString;
    }

    public static string ToStringOr(BsonDocument? doc, string path, string fallback)
    {
        var value = Resolve(doc, path);
        return value is null || value.IsBsonNull ? fallback : value.AsString;
    }

    /// <summary>Достаёт значение по пути вида "employee.fullName".</summary>
    private static BsonValue? Resolve(BsonDocument? doc, string path)
    {
        BsonValue? current = doc;

        foreach (var segment in path.Split('.'))
        {
            if (current is not BsonDocument document || !document.TryGetValue(segment, out var next)) return null;
            current = next;
        }

        return current;
    }
}
