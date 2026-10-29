using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace RateFreak.Api.Middleware;

internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception is BadHttpRequestException { StatusCode: >= 400 and < 500 } rejected
            ? rejected.StatusCode : StatusCodes.Status500InternalServerError;
        if (status == StatusCodes.Status500InternalServerError)
            LogUnhandledException(logger, exception, context.TraceIdentifier);
        await Results.Problem(statusCode: status,
            title: status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred." : ReasonPhrases.GetReasonPhrase(status),
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
            .ExecuteAsync(context);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled request failure. TraceId: {TraceId}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string traceId);
}
