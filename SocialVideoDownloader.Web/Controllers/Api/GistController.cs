using Microsoft.AspNetCore.Mvc;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Services;

namespace SocialVideoDownloader.Web.Controllers.Api;

[ApiController]
[Route("api/gist")]
public sealed class GistController(IGistImportService gist, StatusService status) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var current = await status.GetAsync(cancellationToken);
        return Ok(current.Gist);
    }

    [HttpPost("check")]
    public async Task<IActionResult> Check(CancellationToken cancellationToken) =>
        Ok(await gist.CheckNowAsync(cancellationToken));
}
