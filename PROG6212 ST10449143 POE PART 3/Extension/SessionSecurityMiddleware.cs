using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace PROG6212_ST10449143_POE_PART_1.Extension
{
    public class SessionSecurityMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionSecurityMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Initialize session if needed
            await context.Session.LoadAsync();

            // Set security headers
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

            await _next(context);
        }
    }

    public static class SessionSecurityMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionSecurityMiddleware>();
        }
    }
}
