namespace LearnSpace.Api.Common.Errors;

/// <summary>
/// Thrown by services/controllers for expected failures. The middleware turns it into a ProblemDetails response.
/// </summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static AppException BadRequest(string message) => new(400, message);
    public static AppException Unauthorized(string message = "You are not signed in.") => new(401, message);
    public static AppException Forbidden(string message = "You are not allowed to do this.") => new(403, message);
    public static AppException NotFound(string message = "Not found.") => new(404, message);
    public static AppException Conflict(string message) => new(409, message);
}
