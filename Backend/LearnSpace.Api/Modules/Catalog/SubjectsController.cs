using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LearnSpace.Api.Modules.Identity;

namespace LearnSpace.Api.Modules.Catalog;

[ApiController, Route("api/catalog")]
public class SubjectsController(SubjectService service) : ControllerBase
{
    [HttpGet("subjects")] public async Task<IActionResult> Subjects(CancellationToken ct) => Ok(await service.GetSubjectsAsync(ct));
    [HttpPost("subjects"), Authorize(Roles = AppRoles.Admin)] public async Task<IActionResult> AddSubject(NameRequest request, CancellationToken ct) { try { return Ok(await service.AddSubjectAsync(request.Name, ct)); } catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); } }
    [HttpGet("levels")] public async Task<IActionResult> Levels(CancellationToken ct) => Ok(await service.GetLevelsAsync(ct));
    [HttpGet("subject-levels")] public async Task<IActionResult> SubjectLevels(CancellationToken ct) => Ok(await service.GetSubjectLevelsAsync(ct));
    [HttpPost("levels"), Authorize(Roles = AppRoles.Admin)] public async Task<IActionResult> AddLevel(NameRequest request, CancellationToken ct) { try { return Ok(await service.AddLevelAsync(request.Name, ct)); } catch (InvalidOperationException e) { return BadRequest(new { error = e.Message }); } }
    [HttpPost("subject-levels"), Authorize(Roles = AppRoles.Admin)] public async Task<IActionResult> SubjectLevel(SubjectLevelRequest request, CancellationToken ct) { try { return Ok(await service.AddSubjectLevelAsync(request.SubjectId, request.LevelId, ct)); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { return BadRequest(new { error = e.Message }); } }
}
public record NameRequest(string Name);
public record SubjectLevelRequest(Guid SubjectId, Guid LevelId);
