using ImputationApi.Extensions;
using ImputationApi.Models;

namespace ImputationApi.Services
{
    public sealed class ImputationServiceCacheDecorator(
        ILogger<ImputationServiceCacheDecorator> logger,
        IImputationService inner,
        IRepositoryPathResolver repositoryPathResolver,
        IReferencePanelCacheService referencePanelCacheService) : IImputationService
    {
        private const string DefaultReferencePanelFileName = "reference_panel";

        private readonly ILogger<ImputationServiceCacheDecorator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IImputationService _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        private readonly IRepositoryPathResolver _repositoryPathResolver = repositoryPathResolver ?? throw new ArgumentNullException(nameof(repositoryPathResolver));
        private readonly IReferencePanelCacheService _referencePanelCacheService = referencePanelCacheService ?? throw new ArgumentNullException(nameof(referencePanelCacheService));

        public async Task RunAsync(ImputationJob job, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(job);

            if (job.ReferencePanelDownloadUrl != null && string.IsNullOrWhiteSpace(job.ReferencePanelLocalPath))
            {
                if (string.IsNullOrWhiteSpace(job.RepoWindowsPath))
                {
                    job.RepoWindowsPath = _repositoryPathResolver.ResolveRepositoryWindowsPath().NormalizeWindowsPath();
                }

                string repositoryPath = job.EnsureRepositoryPath();
                Uri referencePanelUrl = job.EnsureReferencePanelUrl();

                string? cachedPath = _referencePanelCacheService.TryGetCachedPath(referencePanelUrl, repositoryPath);
                if (cachedPath != null)
                {
                    _logger.LogInformation("Using cached reference panel for job {JobId}: {Path}", job.Id, cachedPath);
                    job.ReferencePanelLocalPath = cachedPath;
                }
                else
                {
                    string cachedFilePath = await DownloadAndCacheReferencePanelAsync(job, referencePanelUrl, repositoryPath, cancellationToken);
                    _logger.LogInformation("Cached reference panel for job {JobId}: {Path}", job.Id, cachedFilePath);
                    job.ReferencePanelLocalPath = cachedFilePath;
                }
            }

            await _inner.RunAsync(job, cancellationToken);
        }

        private async Task<string> DownloadAndCacheReferencePanelAsync(ImputationJob job, Uri referencePanelUrl, string repositoryPath, CancellationToken cancellationToken)
        {
            string shortId = job.Id.ToString("N")[..8];
            string tempDirectory = Path.Combine(repositoryPath, "downloaded_ref_panel_tmp", shortId);
            _ = Directory.CreateDirectory(tempDirectory);

            string fileName = Path.GetFileName(referencePanelUrl.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = DefaultReferencePanelFileName;
            }

            string tempPath = Path.Combine(tempDirectory, fileName);

            try
            {
                using HttpClient httpClient = new();

                using HttpResponseMessage response = await httpClient.GetAsync(referencePanelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _ = response.EnsureSuccessStatusCode();

                await using (FileStream fileStream = File.Create(tempPath))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }

                string cachedPath = _referencePanelCacheService.SaveToCache(tempPath, referencePanelUrl, repositoryPath);

                return cachedPath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, recursive: true);
                    }
                }
                catch (IOException exception)
                {
                    _logger.LogWarning(exception, "Failed to clean up temporary reference panel directory: {Directory}", tempDirectory);
                }
                catch (UnauthorizedAccessException exception)
                {
                    _logger.LogWarning(exception, "Failed to clean up temporary reference panel directory: {Directory}", tempDirectory);
                }
            }
        }
    }
}
