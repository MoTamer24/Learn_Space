using LearnSpace.Api.Common;
using LearnSpace.Api.Common.Errors;
using LearnSpace.Api.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnSpace.Api.Modules.Content;

[ApiController]
[Route("api")]
public class MaterialsController(MaterialService materials) : ControllerBase
{
    [HttpPost("courses/{courseId:guid}/materials")]
    [Authorize(Roles = AppRoles.Teacher)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MaterialDto>> Upload(
        Guid courseId,
         string title,
         MaterialType? type,
         IFormFile file)
    {
        if (type is null) throw AppException.BadRequest("Type is required (Video or Document).");

        var material = await materials.UploadAsync(User.GetUserId(), courseId, title, type.Value, file);
        return StatusCode(201, material);
    }

    [HttpGet("courses/{courseId:guid}/materials")]
    [Authorize]
    public async Task<ActionResult<List<MaterialDto>>> List(Guid courseId)
        => await materials.ListAsync(User.GetUserId(), courseId);

    /// <summary>Streams the file. Range processing is on so video seeking works.</summary>
    [HttpGet("materials/{id:guid}/content")]
    [Authorize]
    public async Task<IActionResult> GetContent(Guid id)
    {
        var (stream, contentType) = await materials.OpenAsync(User.GetUserId(), id);
        return File(stream, contentType, enableRangeProcessing: true);
    }
}
