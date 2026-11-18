using System.Text.Json;
namespace RateFreak.Api.Endpoints;

// Notification commands take no parameters: reject payloads that could imply a different scope.
internal sealed class NotificationBodyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > 65_536) return Results.StatusCode(413);
        var bytes = new byte[65_537]; var size = 0;
        while (size < bytes.Length)
        {
            var read = await request.Body.ReadAsync(bytes.AsMemory(size), context.HttpContext.RequestAborted);
            if (read == 0) break; size += read;
        }
        if (size > 65_536) return Results.StatusCode(413);
        if (size != 0)
        {
            if (!request.HasJsonContentType()) return Results.StatusCode(415);
            try
            {
                using var document = JsonDocument.Parse(bytes.AsMemory(0, size));
                if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any())
                    return Results.BadRequest();
            }
            catch (JsonException) { return Results.BadRequest(); }
        }
        return await next(context);
    }
}
