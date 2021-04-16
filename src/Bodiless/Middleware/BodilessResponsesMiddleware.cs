using System.Threading.Tasks;
using Bodiless.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace Bodiless.Middleware
{
    public class BodilessResponsesMiddleware
    {
        private readonly BodilessOptions _options;
        private readonly RequestDelegate _next;

        public BodilessResponsesMiddleware(RequestDelegate next)
        {
            _options = new BodilessOptions { RequiredHeader = "Discard-Body", RequiredValue = "true" };
            _next = next;
        }

        public BodilessResponsesMiddleware(RequestDelegate next, BodilessOptions options = null)
        {
            _options = options;
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (RequiredHeaderExists(context) && HeaderHasRequiredValue(context))
            {
                await using var voidResponse = new VoidStream(context.Response.Body);
                context.Response.Body = voidResponse;
            }

            await _next(context);
        }

        private bool RequiredHeaderExists(HttpContext context)
        {
            return context.Request.Headers.ContainsKey(_options.RequiredHeader);
        }

        private bool HeaderHasRequiredValue(HttpContext context)
        {
            return _options.RequiredValue == null || context.Request.Headers[_options.RequiredHeader] == _options.RequiredValue;
        }
    }
}