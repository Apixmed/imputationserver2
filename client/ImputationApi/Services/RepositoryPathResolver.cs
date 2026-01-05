namespace ImputationApi.Services
{
    public sealed class RepositoryPathResolver(IWebHostEnvironment environment) : IRepositoryPathResolver
    {
        private readonly IWebHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));

        public string ResolveRepositoryWindowsPath()
        {
            string contentRootPath = _environment.ContentRootPath;
            DirectoryInfo? clientDirectory = Directory.GetParent(contentRootPath)
                ?? throw new InvalidOperationException("Unable to resolve client directory from content root path: " + contentRootPath);

            DirectoryInfo? repositoryRootDirectory = clientDirectory.Parent;

            return repositoryRootDirectory == null
                ? throw new InvalidOperationException("Unable to resolve repository root directory from client directory: " + clientDirectory.FullName)
                : repositoryRootDirectory.FullName;
        }
    }
}


