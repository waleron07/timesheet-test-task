using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Сериализация DateOnly в BSON-дату (UTC-полночь) и обратно.
///
/// Собственный сериализатор, потому что MongoDB.Driver 2.x не поддерживает
/// DateOnly из коробки. Альтернативой было хранить дату строкой «2026-03-05»,
/// но тогда диапазонные запросы по месяцу работали бы лексикографически, а
/// не по времени — рабочее, но хрупкое решение, ломающееся на любом другом
/// формате даты.
///
/// Полночь именно в UTC: дата табеля — календарная, без времени и без
/// таймзоны. Если бы полночь считалась локальной, запись за 01.03 на сервере
/// с положительным смещением легла бы в базу как 28.02T21:00Z и попала бы в
/// отчёт за февраль.
/// </summary>
public sealed class DateOnlyAsUtcDateSerializer : StructSerializerBase<DateOnly>
{
    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        var type = reader.GetCurrentBsonType();

        return type switch
        {
            BsonType.DateTime => DateOnly.FromDateTime(
                BsonUtils.ToDateTimeFromMillisecondsSinceEpoch(reader.ReadDateTime())),

            // Подстраховка на случай документов, залитых как строка ISO
            // (например, скриптом миграции) — читаем, но не пишем так.
            BsonType.String => DateOnly.ParseExact(reader.ReadString(), "yyyy-MM-dd"),

            _ => throw new FormatException(
                $"Не удалось прочитать DateOnly из BSON-типа {type}: ожидались DateTime или String.")
        };
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value)
    {
        var utcMidnight = value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        context.Writer.WriteDateTime(BsonUtils.ToMillisecondsSinceEpoch(utcMidnight));
    }
}
