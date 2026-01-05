using ImputationApi.Extensions;

namespace ImputationApi.Services
{
    public sealed class ReferencePanelCacheService(ILogger<ReferencePanelCacheService> logger) : IReferencePanelCacheService
    {
        private const string CacheDirectoryName = "reference_panel_cache";

        private readonly ILogger<ReferencePanelCacheService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public string? TryGetCachedPath(Uri referencePanelUrl, string repositoryPath)
        {
            ArgumentNullException.ThrowIfNull(referencePanelUrl);
            ArgumentNullException.ThrowIfNull(repositoryPath);

            string cacheRootDirectory = Path.Combine(repositoryPath, CacheDirectoryName);
            string stableUrl = referencePanelUrl.GetLeftPart(UriPartial.Path);
            string cacheKey = ChecksumExtensions.ComputeSha256Hex(stableUrl);

            string fileName = Path.GetFileName(referencePanelUrl.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "reference_panel";
            }

            string cachedDirectory = Path.Combine(cacheRootDirectory, cacheKey);
            string cachedFilePath = Path.Combine(cachedDirectory, fileName);
            string checksumFilePath = cachedFilePath + ".sha256";

            if (!File.Exists(cachedFilePath))
            {
                return null;
            }

            try
            {
                string actualChecksum = ChecksumExtensions.ComputeFileSha256Hex(cachedFilePath);
                if (!File.Exists(checksumFilePath))
                {
                    File.WriteAllText(checksumFilePath, actualChecksum);
                    return cachedFilePath;
                }

                string expectedChecksum = File.ReadAllText(checksumFilePath).Trim();
                if (string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    return cachedFilePath;
                }

                File.Delete(cachedFilePath);
                File.Delete(checksumFilePath);
                _logger.LogWarning("Cached reference panel checksum mismatch, deleted: {Path}", cachedFilePath);

                return null;
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "Failed to validate cached reference panel checksum: {Path}", cachedFilePath);

                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(exception, "Failed to validate cached reference panel checksum: {Path}", cachedFilePath);

                return null;
            }
        }

        public string SaveToCache(string sourceFilePath, Uri referencePanelUrl, string repositoryPath)
        {
            ArgumentNullException.ThrowIfNull(sourceFilePath);
            ArgumentNullException.ThrowIfNull(referencePanelUrl);
            ArgumentNullException.ThrowIfNull(repositoryPath);

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Source file not found.", sourceFilePath);
            }

            string cacheRootDirectory = Path.Combine(repositoryPath, CacheDirectoryName);
            string stableUrl = referencePanelUrl.GetLeftPart(UriPartial.Path);
            string cacheKey = ChecksumExtensions.ComputeSha256Hex(stableUrl);

            string fileName = Path.GetFileName(referencePanelUrl.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "reference_panel";
            }

            string cachedDirectory = Path.Combine(cacheRootDirectory, cacheKey);
            string cachedFilePath = Path.Combine(cachedDirectory, fileName);
            string checksumFilePath = cachedFilePath + ".sha256";

            try
            {
                _ = Directory.CreateDirectory(cachedDirectory);
                File.Copy(sourceFilePath, cachedFilePath, overwrite: true);

                string checksum = ChecksumExtensions.ComputeFileSha256Hex(cachedFilePath);
                File.WriteAllText(checksumFilePath, checksum);
                _logger.LogInformation("Saved reference panel to cache: {Path}", cachedFilePath);

                return cachedFilePath;
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "Failed to save reference panel to cache: {Path}", cachedFilePath);

                throw;
            }
        }
    }
}

