using Bodiless.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Bodiless.Middleware;

public sealed class BodilessResponsesMiddleware(RequestDelegate next, BodilessOptions? options = null)
{
    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly BodilessOptions options = options ?? new();

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldDiscardBody(context))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var bodilessBody = new VoidStream(originalBody);
        context.Response.OnStarting(static state =>
        {
            ((HttpResponse)state).ContentLength = 0;
            return Task.CompletedTask;
        }, context.Response);
        context.Response.Body = bodilessBody;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private bool ShouldDiscardBody(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(options.RequiredHeader, out var values))
        {
            return false;
        }

        return options.RequiredValue is null || HasRequiredValue(values);
    }

    private bool HasRequiredValue(StringValues values)
    {
        foreach (var value in values)
        {
            if (string.Equals(value, options.RequiredValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
