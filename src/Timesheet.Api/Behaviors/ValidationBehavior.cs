using FluentValidation;
using MediatR;

namespace Timesheet.Api.Behaviors;

/// <summary>
/// Прогоняет FluentValidation-валидаторы перед обработчиком.
///
/// Благодаря этому валидация формата входных данных физически отделена от
/// бизнес-правил: валидатор отвечает на вопрос «запрос осмыслен», обработчик —
/// «операция допустима в текущем состоянии базы».
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validators = _validators.ToList();
        if (validators.Count == 0) return await next();

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0) throw new ValidationException(failures);

        return await next();
    }
}
