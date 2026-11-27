using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Identity;

public record CreateIdentityUserRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, StringLength(100)] string Name,
    [property: Required, StringLength(8, MinimumLength = 8)] string Password,
    [property: Required] string Role,
    string? Phone);

public record CreateStudentRequest(Guid UserId, string? Code);
public record CreateParentRequest(Guid UserId);
public record LinkParentRequest(Guid StudentId, Guid ParentId, string Relation, bool IsPrimary);
public record CreateStaffRequest(Guid UserId, Guid TeacherId, string JobTitle);
public record StudentProfileDto(Guid Id, Guid UserId, string Name, string Code);
