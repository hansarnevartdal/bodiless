using Bodiless.Middleware;
using Microsoft.AspNetCore.Builder;

namespace Bodiless.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseBodilessResponses(this IApplicationBuilder builder, BodilessOptions options = null)
        {
            return builder.UseMiddleware<BodilessResponsesMiddleware>(options);
        }
    }
}