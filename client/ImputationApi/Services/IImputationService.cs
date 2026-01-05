using ImputationApi.Models;

namespace ImputationApi.Services
{
    public interface IImputationService
    {
        Task RunAsync(ImputationJob job, CancellationToken cancellationToken);
    }
}
