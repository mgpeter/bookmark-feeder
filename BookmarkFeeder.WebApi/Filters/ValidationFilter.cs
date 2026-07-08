using FluentValidation;

namespace BookmarkFeeder.WebApi.Filters;

/// <summary>
/// Runs the registered FluentValidation validator for the first argument of type
/// <typeparamref name="T"/> and short-circuits with a 400 ValidationProblem on failure.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is not null)
        {
            var argument = context.Arguments.OfType<T>().FirstOrDefault();
            if (argument is not null)
            {
                var result = await validator.ValidateAsync(argument);
                if (!result.IsValid)
                {
                    return TypedResults.ValidationProblem(result.ToDictionary());
                }
            }
        }

        return await next(context);
    }
}
