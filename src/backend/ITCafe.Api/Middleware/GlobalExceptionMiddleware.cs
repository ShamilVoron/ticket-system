using System.Net;
using System.Text.Json;

namespace ITCafe.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var isDevelopment = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true;

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException _ => (HttpStatusCode.Forbidden, isDevelopment ? exception.Message : "Forbidden"),
            InvalidOperationException _ => (HttpStatusCode.Conflict, isDevelopment ? exception.Message : "A conflict occurred."),
            ArgumentException _ => (HttpStatusCode.BadRequest, isDevelopment ? exception.Message : "Bad request."),
            KeyNotFoundException _ => (HttpStatusCode.NotFound, "Resource not found"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = context.Response.StatusCode,
            message,
            stackTrace = isDevelopment ? exception.StackTrace : null
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
