using SocialVideoDownloader.Core.DTOs;

namespace SocialVideoDownloader.Core.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(string name, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task<CategoryDto> SetDefaultAsync(int id, CancellationToken cancellationToken);

    Task<string> ResolveFolderAsync(int? categoryId, CancellationToken cancellationToken);
}
