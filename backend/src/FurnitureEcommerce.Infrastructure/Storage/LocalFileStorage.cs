using FurnitureEcommerce.Application.Common;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Infrastructure.Storage;

public class LocalFileStorage(
    string rootPath,
    string baseUrl,
    ILogger<LocalFileStorage> logger) : IFileStorage
{
    public async Task<string> SaveAsync(
        Stream content,
        string folder,
        string extension,
        CancellationToken cancellationToken)
    {
        // The name is generated here so that nothing from the client reaches the file system.
        var relativePath = $"{folder}/{Guid.CreateVersion7():N}{extension}";
        var fullPath = GetFullPath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, cancellationToken);

        return relativePath;
    }

    public Task DeleteAsync(string relativePath)
    {
        try
        {
            File.Delete(GetFullPath(relativePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not delete file {Path}", relativePath);
        }

        return Task.CompletedTask;
    }

    public string GetUrl(string relativePath) => $"{baseUrl.TrimEnd('/')}/{relativePath}";

    private string GetFullPath(string relativePath) => Path.Combine(rootPath, relativePath);
}
