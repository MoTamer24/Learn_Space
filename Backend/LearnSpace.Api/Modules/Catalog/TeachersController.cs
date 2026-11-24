using LearnSpace.Api.Common;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Modules.Catalog;

[ApiController]
[Route("api/teachers")]
public class TeachersController(TeacherService teachers, CourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TeacherDto>>> List()
        => await teachers.ListAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeacherDetailDto>> Get(Guid id)
        => await teachers.GetAsync(id);

    [HttpGet("{id:guid}/logo")]
    public async Task<IActionResult> GetLogo(Guid id)
    {
        var (content, contentType) = await teachers.OpenLogoAsync(id);
        return File(content, contentType);
    }

    [HttpPut("me/profile")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<TeacherDto>> UpdateProfile(UpdateProfileRequest request)
        => await teachers.UpdateProfileAsync(User.GetUserId(), request);

    [HttpPost("me/logo")]
    [Authorize(Roles = AppRoles.Teacher)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TeacherDto>> UploadLogo( IFormFile file)
        => await teachers.UploadLogoAsync(User.GetUserId(), file);

    [HttpGet("me/courses")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<List<CourseDto>>> MyCourses()
        => await courses.ListMineAsync(User.GetUserId());
}
