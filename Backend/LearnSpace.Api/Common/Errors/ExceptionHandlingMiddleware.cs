using System.Text.Json;
using LearnSpace.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Common.Errors;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            var userId = context.User.TryGetUserId();
            logger.LogWarning(
                "AppException ({StatusCode}) at {Path} for user {UserId}: {Message}",
                ex.StatusCode,
                context.Request.Path,
                userId?.ToString() ?? "Anonymous",
                ex.Message);

            await WriteProblemAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            var userId = context.User.TryGetUserId();
            logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path} for user {UserId}",
                context.Request.Method,
                context.Request.Path,
                userId?.ToString() ?? "Anonymous");

            await WriteProblemAsync(context, 500, "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        if (context.Response.HasStarted) return;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = status switch
            {
                400 => "Invalid input",
                401 => "Not signed in",
                403 => "Not allowed",
                404 => "Not found",
                409 => "Rule violation",
                _ => "Server error"
            },
            Detail = detail
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json");
    }
}
