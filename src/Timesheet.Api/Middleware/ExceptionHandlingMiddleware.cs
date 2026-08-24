using System.Text.Json;
using FluentValidation;
using Timesheet.Api.Contracts;
using Timesheet.Domain;

namespace Timesheet.Api.Middleware;

/// <summary>
/// Единственное место, где исключение превращается в HTTP-ответ.
///
/// Без него нарушение бизнес-правила уехало бы клиенту как 500 с текстом
/// исключения — ровно то, что ТЗ запрещает, и ровно то, что делал исходный
/// код из части 1 (там нарушение правил вообще заканчивалось NRE).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            // Ошибка формата запроса: поля не прошли FluentValidation.
            // Это ещё не бизнес-правило — до базы дело не дошло.
            var details = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await WriteAsync(context, StatusCodes.Status400BadRequest,
                new ApiError("VALIDATION_FAILED", "Проверьте правильность заполнения полей.", details));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogInformation("Нарушение бизнес-правила {Code}: {Message}", ex.Code, ex.Message);

            await WriteAsync(context, StatusCodeFor(ex.Code), new ApiError(ex.Code, ex.Message));
        }
        catch (Exception ex)
        {
            // Всё остальное — настоящий сбой. Клиенту наружу текст исключения
            // не отдаём, но в лог пишем целиком.
            _logger.LogError(ex, "Необработанная ошибка при {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                new ApiError("INTERNAL_ERROR", "Внутренняя ошибка сервера. Попробуйте повторить запрос."));
        }
    }

    /// <summary>
    /// 400 — запрос неверен сам по себе и не станет верным без правки данных.
    /// 409 — запрос корректен, но конфликтует с текущим состоянием системы:
    ///       клиенту имеет смысл перечитать данные и попробовать снова.
    /// 404 — ссылка на несуществующую сущность.
    /// </summary>
    private static int StatusCodeFor(string code) => code switch
    {
        ErrorCodes.InvalidHours => StatusCodes.Status400BadRequest,
        ErrorCodes.RateNotFound => StatusCodes.Status400BadRequest,
        ErrorCodes.DateOutOfProjectRange => StatusCodes.Status400BadRequest,

        ErrorCodes.DailyLimitExceeded => StatusCodes.Status409Conflict,
        ErrorCodes.PeriodClosed => StatusCodes.Status409Conflict,
        ErrorCodes.ConcurrentModification => StatusCodes.Status409Conflict,

        ErrorCodes.EmployeeNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.ProjectNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.TimeEntryNotFound => StatusCodes.Status404NotFound,

        _ => StatusCodes.Status400BadRequest
    };

    private static async Task WriteAsync(HttpContext context, int statusCode, ApiError error)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }
}
