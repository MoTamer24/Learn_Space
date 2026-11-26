using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Identity;

public record RegisterRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, MinLength(8), StringLength(100)] string Password,
    [property: Required, StringLength(100)] string DisplayName,
    [property: Required] string Role,
    [property: Phone] string? Phone = null);

public record LoginRequest(
    [property: Required] string Email,
    [property: Required] string Password);

public record RegisteredUserDto(Guid UserId, string Email, string DisplayName, string Role);

public record LoginResponse(string AccessToken, DateTime ExpiresAt, Guid UserId, string DisplayName, string Role);
