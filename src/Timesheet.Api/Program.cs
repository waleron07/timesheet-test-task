using MongoDB.Bson;
using MongoDB.Driver;
using Timesheet.Api;

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
