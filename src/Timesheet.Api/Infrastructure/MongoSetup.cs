using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Timesheet.Domain;

namespace Timesheet.Api.Infrastructure;

/// <summary>
/// Настройка сериализации доменных сущностей в MongoDB.
///
/// Маппинг задаётся здесь, а не атрибутами на классах домена: домен не должен
/// зависеть от драйвера базы. Отдельного слоя документов при этом нет —
/// сущности маленькие, и третий набор классов дал бы больше кода, чем пользы.
/// Граница, которая действительно важна, — «модель БД ≠ DTO ответа API»:
/// контракт API описан отдельными типами и не ломается при переименовании
/// поля в базе.
/// </summary>
public static class MongoSetup
{
    private static bool _registered;
    private static readonly object Lock = new();

    public static void Register()
    {
        // Регистрация сериализаторов глобальна и одноразова: повторный вызов
        // бросает исключение, поэтому защищаемся от повторной инициализации
        // (например, в тестах, где хост поднимается несколько раз).
        lock (Lock)
        {
            if (_registered) return;
            _registered = true;

            ConventionRegistry.Register("timesheet", new ConventionPack
            {
                // camelCase в базе, PascalCase в C#.
                new CamelCaseElementNameConvention(),
                // Новое поле в документе не должно ронять десериализацию
                // на старом коде — важно при выкатке без остановки.
                new IgnoreExtraElementsConvention(true)
            }, _ => true);

            // Деньги — Decimal128, а не double. Регистрация глобальная, чтобы
            // ни одно денежное поле не уехало в базу как двоичная дробь просто
            // потому, что кто-то забыл атрибут.
            BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.RegisterSerializer(
                new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

            // Дата табеля — календарная дата без времени. В базе лежит как
            // UTC-полночь: иначе смещение таймзоны сервера переносит записи
            // первого и последнего числа в соседний месяц.
            BsonSerializer.RegisterSerializer(new DateOnlyAsUtcDateSerializer());
            BsonSerializer.RegisterSerializer(
                new NullableSerializer<DateOnly>(new DateOnlyAsUtcDateSerializer()));

            RegisterClassMaps();
        }
    }

    private static void RegisterClassMaps()
    {
        BsonClassMap.RegisterClassMap<Employee>(map =>
        {
            map.AutoMap();
            map.MapIdMember(e => e.Id);
        });

        BsonClassMap.RegisterClassMap<Project>(map =>
        {
            map.AutoMap();
            map.MapIdMember(p => p.Id);
        });

        BsonClassMap.RegisterClassMap<TimeEntry>(map =>
        {
            map.AutoMap();
            map.MapIdMember(e => e.Id);
        });

        BsonClassMap.RegisterClassMap<ClosedPeriod>(map =>
        {
            map.AutoMap();
            map.MapIdMember(p => p.Id);
        });

        BsonClassMap.RegisterClassMap<Rate>(map => map.AutoMap());
    }
}
