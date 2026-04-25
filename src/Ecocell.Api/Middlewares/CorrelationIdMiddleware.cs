using Serilog.Context;

namespace Ecocell.Api.Middlewares;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
            correlationId = Guid.NewGuid().ToString();

        httpContext.Response.Headers.Append(CorrelationIdHeader, correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(httpContext);
        }
    }
}
