using System.ComponentModel.DataAnnotations;

namespace LearnSpace.Api.Modules.Catalog;

public record CourseDto(
    Guid Id, Guid TeacherId, string TeacherName, string Title, string Description, string Subject, CourseStatus Status);

public record CreateCourseRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(5000)] string Description,
    [property: Required, StringLength(100)] string Subject);

public record UpdateCourseRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(5000)] string Description,
    [property: Required, StringLength(100)] string Subject);

public record TeacherDto(Guid Id, string DisplayName, string Bio, string AccentColor, string? LogoUrl);

public record TeacherDetailDto(
    Guid Id, string DisplayName, string Bio, string AccentColor, string? LogoUrl, List<CourseDto> Courses);

public record UpdateProfileRequest(
    [property: StringLength(2000)] string? Bio,
    [property: Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "AccentColor must look like #0F766E.")] string AccentColor);
