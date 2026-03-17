using Bodiless.Middleware;
using Microsoft.AspNetCore.Builder;

namespace Bodiless.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseBodilessResponses(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.UseMiddleware<BodilessResponsesMiddleware>();
    }

    public static IApplicationBuilder UseBodilessResponses(this IApplicationBuilder builder, BodilessOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        return builder.UseMiddleware<BodilessResponsesMiddleware>(options);
    }
}
