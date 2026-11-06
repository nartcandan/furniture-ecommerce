namespace FurnitureEcommerce.Application.Common;

public interface IFileStorage
{
    /// <summary>Saves the content under a generated file name.</summary>
    /// <returns>The relative path of the saved file.</returns>
    Task<string> SaveAsync(
        Stream content,
        string folder,
        string extension,
        CancellationToken cancellationToken);

    /// <summary>Deletes the file. A file that cannot be deleted is left behind and does not fail the call.</summary>
    Task DeleteAsync(string relativePath);

    string GetUrl(string relativePath);
}
