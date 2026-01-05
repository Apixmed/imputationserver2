namespace ImputationApi.Services
{
    public interface IReferencePanelCacheService
    {
        string? TryGetCachedPath(Uri referencePanelUrl, string repositoryPath);

        string SaveToCache(string sourceFilePath, Uri referencePanelUrl, string repositoryPath);
    }
}
