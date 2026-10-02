using System.Threading;
using System.Threading.Tasks;

namespace SabersCore.Services;

public interface ISaberDirectoryReadiness
{
    Task WaitForDirectoriesAsync(CancellationToken token);
    Task EnsureDirectoriesAsync(string[] paths, CancellationToken token);
}
