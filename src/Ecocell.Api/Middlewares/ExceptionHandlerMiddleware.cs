using Ecocell.Shared.Responses;

namespace Ecocell.Api.Middlewares;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {            
            await _next(httpContext);
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Ocorreu um erro ao fazer a solicitação: {@Error}", ex);
            await HandleException(httpContext, ex);
        }
    }

    private Task HandleException(HttpContext httpContext, Exception exception)
    {
        var errorMessage = new ResponseError("Ocorreu um erro desconhecido no servidor.");
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return httpContext.Response.WriteAsJsonAsync(errorMessage);
    }    
}
