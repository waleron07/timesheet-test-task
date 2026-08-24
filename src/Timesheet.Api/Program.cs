using FluentValidation;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api;
using Timesheet.Api.Behaviors;
using Timesheet.Api.Features.TimeEntries;
using Timesheet.Api.Infrastructure;
using Timesheet.Api.Middleware;

// Сериализаторы и конвенции регистрируются до создания клиента: драйвер
// кэширует сериализаторы при первом обращении к типу, и поздняя регистрация
// молча не применится.
MongoSetup.Register();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));

// IMongoClient — потокобезопасен и держит пул соединений, поэтому singleton.
// Создавать клиент на запрос — классический способ исчерпать пул.
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var options = sp.GetRequiredService<IConfiguration>().GetSection("Mongo").Get<MongoOptions>()
                  ?? new MongoOptions();
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IConfiguration>().GetSection("Mongo").Get<MongoOptions>()
                  ?? new MongoOptions();
    return sp.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
});

builder.Services.AddSingleton<TimesheetCollections>();
builder.Services.AddSingleton<DatabaseSeeder>();
builder.Services.AddScoped<TimeEntryGuard>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MongoOptions).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(MongoOptions).Assembly);

// Валидаторы прогоняются до обработчика: проверка формата отделена от
// бизнес-правил, как требует ТЗ.
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Фронт в dev-режиме живёт на другом порту (Vite), поэтому CORS.
// В проде фронт раздаётся с того же origin и CORS не нужен.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Режим наполнения базы: `dotnet Timesheet.Api.dll seed`.
// Отдельной командой, а не автоматически при старте: сид сносящий, и запускать
// его втихую при каждом рестарте контейнера означало бы терять данные.
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var collections = scope.ServiceProvider.GetRequiredService<TimesheetCollections>();
    await MongoIndexes.EnsureAsync(collections);
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    return;
}

// Индексы создаются на старте, идемпотентно.
await MongoIndexes.EnsureAsync(app.Services.GetRequiredService<TimesheetCollections>());

// Middleware ошибок — первым в конвейере, чтобы поймать всё, что ниже.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

// Healthcheck: заодно проверяет, что Mongo реально отвечает,
// а не только что процесс API поднялся.
app.MapGet("/api/health", async (IMongoDatabase db, CancellationToken token) =>
{
    try
    {
        await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: token);
        return Results.Ok(new { status = "ok", mongo = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "degraded", mongo = ex.Message }, statusCode: 503);
    }
});

app.Run();
