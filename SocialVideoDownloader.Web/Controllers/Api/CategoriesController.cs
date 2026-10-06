using Microsoft.AspNetCore.Mvc;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Interfaces;

namespace SocialVideoDownloader.Web.Controllers.Api;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await categories.CreateAsync(request.Name ?? string.Empty, cancellationToken);
            return Created($"/api/categories/{created.Id}", created);
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    [HttpPost("{id:int}/default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await categories.SetDefaultAsync(id, cancellationToken));
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await categories.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    public sealed class CategoryRequest
    {
        public string? Name { get; set; }
    }
}
