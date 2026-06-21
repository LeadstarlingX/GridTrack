using FluentValidation;
using GridTrack.Application.Abstractions.Validation;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = GridTrack.Application.Abstractions.Validation.ValidationException;

namespace GridTrack.Presentation.Validation;

/// <summary>
/// Runs any registered FluentValidation validator for each action argument at the HTTP
/// boundary. On failure it throws the application's <see cref="ValidationException"/>, which
/// <c>ExceptionHandlingMiddleware</c> maps to <c>400 ValidationFailure</c> with an "errors" array.
/// Without this filter that ValidationException → 400 branch is unreachable.
/// </summary>
public sealed class ValidationActionFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new List<ValidationError>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            // Resolve IValidator<TArg> if one is registered for this argument's runtime type.
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (!result.IsValid)
                errors.AddRange(result.Errors.Select(f => new ValidationError(f.PropertyName, f.ErrorMessage)));
        }

        if (errors.Count > 0)
            throw new ValidationException(errors);

        await next();
    }
}
