using LearnSpace.Api.Common;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Modules.Catalog;

[ApiController]
[Route("api/courses")]
public class CoursesController(CourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CourseDto>>> List([FromQuery] string? subject)
        => await courses.ListPublishedAsync(subject);

    // Public for Published courses. The owner can also open their own Draft (token is read if present).
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CourseDto>> Get(Guid id)
        => await courses.GetAsync(id, User.TryGetUserId());

    [HttpPost]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<CourseDto>> Create(CreateCourseRequest request)
    {
        var course = await courses.CreateAsync(User.GetUserId(), request);
        return CreatedAtAction(nameof(Get), new { id = course.Id }, course);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<CourseDto>> Update(Guid id, UpdateCourseRequest request)
        => await courses.UpdateAsync(User.GetUserId(), id, request);

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = AppRoles.Teacher)]
    public async Task<ActionResult<CourseDto>> Publish(Guid id)
        => await courses.PublishAsync(User.GetUserId(), id);
}
