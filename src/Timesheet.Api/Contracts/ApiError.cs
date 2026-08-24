namespace Timesheet.Api.Contracts;

/// <summary>
/// Тело ответа при ошибке. Машиночитаемый код плюс человекочитаемый текст
/// на русском — как требует ТЗ. Фронт различает ситуации по code, а не по
/// разбору строки сообщения.
/// </summary>
/// <param name="Code">Код из <see cref="Timesheet.Domain.ErrorCodes"/>.</param>
/// <param name="Message">Текст для пользователя.</param>
/// <param name="Details">Разбор по полям — только для ошибок валидации входа.</param>
public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null);
