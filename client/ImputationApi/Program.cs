using ImputationApi.Services;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace ImputationApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Services.AddApplicationInsightsTelemetry();
            builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);

            builder.Services.AddControllers();
            builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
            builder.Services.AddSingleton<IRepositoryPathResolver, RepositoryPathResolver>();
            builder.Services.AddSingleton<IReferencePanelCacheService, ReferencePanelCacheService>();
            builder.Services.AddSingleton<ImputationService>();
            builder.Services.AddSingleton<IImputationService>(serviceProvider =>
            {
                ILogger<ImputationServiceCacheDecorator> logger = serviceProvider.GetRequiredService<ILogger<ImputationServiceCacheDecorator>>();
                ImputationService inner = serviceProvider.GetRequiredService<ImputationService>();
                IRepositoryPathResolver repositoryPathResolver = serviceProvider.GetRequiredService<IRepositoryPathResolver>();
                IReferencePanelCacheService cache = serviceProvider.GetRequiredService<IReferencePanelCacheService>();

                return new ImputationServiceCacheDecorator(logger, inner, repositoryPathResolver, cache);
            });

            WebApplication app = builder.Build();

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
