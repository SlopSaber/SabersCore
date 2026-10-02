using System.Threading;
using System.Threading.Tasks;
using SabersCore.Models;

namespace SabersCore.Services;

public interface ISaberMetadataSnapshotCache : ISaberMetadataCache
{
    Task<CustomSaberMetadata[]> GetRefreshedMetadataAsync(CancellationToken token);
}
