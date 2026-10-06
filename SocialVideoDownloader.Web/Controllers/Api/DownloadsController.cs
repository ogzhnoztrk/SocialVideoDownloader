using System.Text;
using Microsoft.AspNetCore.Mvc;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Web.Models;

namespace SocialVideoDownloader.Web.Controllers.Api;

[ApiController]
[Route("api/downloads")]
public sealed class DownloadsController(IDownloadJobService jobs, IUrlListImportService lists) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await jobs.ListAsync(skip, take, cancellationToken));

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken cancellationToken) =>
        Ok(await jobs.ListActiveAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, cancellationToken);
        return job is null ? NotFound(new { message = "İndirme bulunamadı." }) : Ok(job);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UrlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await jobs.CreateAsync(
                request.Url ?? string.Empty,
                cancellationToken,
                DownloadSource.Manual,
                request.CategoryId);
            return Created($"/api/downloads/{created.Job.Id}", created);
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    [HttpPost("import")]
    [RequestSizeLimit(300_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000)]
    public async Task<IActionResult> Import(IFormFile? file, [FromForm] int? categoryId, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "TXT dosyası seçin." });

        if (file.Length > 200_000 || !string.Equals(Path.GetExtension(file.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Yalnızca 200 KB altındaki .txt dosyası yüklenebilir." });

        string content;
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            content = await reader.ReadToEndAsync(cancellationToken);

        if (content.Contains('\0'))
            return BadRequest(new { message = "Dosya metin değil." });

        try
        {
            var result = await lists.ImportAsync(content, categoryId, DownloadSource.File, "[Dosya]", cancellationToken);
            if (result.Found == 0)
                result.Message = result.Invalid == 0 ? "Dosyada video adresi yok." : "Dosyada geçerli video adresi yok.";
            return Ok(result);
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.CancelAsync(id, cancellationToken);
        return job is null ? NotFound(new { message = "İndirme bulunamadı." }) : Ok(job);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.RetryAsync(id, cancellationToken);
        return job is null ? NotFound(new { message = "İndirme bulunamadı." }) : Ok(job);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool deleteFile = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await jobs.DeleteAsync(id, deleteFile, cancellationToken);
            return deleted ? NoContent() : NotFound(new { message = "İndirme bulunamadı." });
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    [HttpPost("{id:guid}/open-file")]
    public async Task<IActionResult> OpenFile(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await jobs.OpenFileAsync(id, cancellationToken);
            return Ok(new { message = "Dosya açıldı." });
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }

    [HttpPost("{id:guid}/open-folder")]
    public async Task<IActionResult> OpenFolder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await jobs.OpenFolderAsync(id, cancellationToken);
            return Ok(new { message = "Klasör açıldı." });
        }
        catch (DownloadException exception)
        {
            return ApiResults.FromException(exception);
        }
    }
}
