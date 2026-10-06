using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Files;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class CategoryService(IDbContextFactory<AppDbContext> dbFactory) : ICategoryService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureDefaultAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.DownloadCategories.AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<CategoryDto> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var folder = CategoryFolder.Normalize(name);
        await EnsureDefaultAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var names = await db.DownloadCategories.Select(category => category.Name).ToListAsync(cancellationToken);
        if (names.Any(existing => existing.Equals(folder, StringComparison.OrdinalIgnoreCase)))
            throw new DownloadException("Bu kategori zaten var.");

        var category = new DownloadCategory
        {
            Name = folder,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.DownloadCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return Map(category);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.DownloadCategories.ToListAsync(cancellationToken);
        var category = items.FirstOrDefault(item => item.Id == id)
            ?? throw new DownloadException("Kategori bulunamadı.");
        if (items.Count == 1)
            throw new DownloadException("Son kategori silinemez.");

        if (category.IsDefault)
        {
            var next = items.Where(item => item.Id != id).OrderBy(item => item.CreatedAt).First();
            next.IsDefault = true;
        }

        db.DownloadCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CategoryDto> SetDefaultAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.DownloadCategories.ToListAsync(cancellationToken);
        var selected = items.FirstOrDefault(item => item.Id == id)
            ?? throw new DownloadException("Kategori bulunamadı.");
        foreach (var item in items)
            item.IsDefault = item.Id == id;

        await db.SaveChangesAsync(cancellationToken);
        return Map(selected);
    }

    public async Task<string> ResolveFolderAsync(int? categoryId, CancellationToken cancellationToken)
    {
        await EnsureDefaultAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (categoryId is int id)
        {
            var selected = await db.DownloadCategories.AsNoTracking()
                .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);
            if (selected is null)
                throw new DownloadException("Kategori bulunamadı.");

            return selected.Name;
        }

        var fallback = await db.DownloadCategories.AsNoTracking()
            .OrderByDescending(category => category.IsDefault)
            .ThenBy(category => category.Id)
            .FirstAsync(cancellationToken);
        return fallback.Name;
    }

    private async Task EnsureDefaultAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            if (await db.DownloadCategories.AnyAsync(cancellationToken))
            {
                if (!await db.DownloadCategories.AnyAsync(category => category.IsDefault, cancellationToken))
                {
                    var first = await db.DownloadCategories.OrderBy(category => category.Id).FirstAsync(cancellationToken);
                    first.IsDefault = true;
                    await db.SaveChangesAsync(cancellationToken);
                }

                return;
            }

            db.DownloadCategories.Add(new DownloadCategory
            {
                Name = CategoryFolder.DefaultName,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static CategoryDto Map(DownloadCategory category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        IsDefault = category.IsDefault,
    };
}
