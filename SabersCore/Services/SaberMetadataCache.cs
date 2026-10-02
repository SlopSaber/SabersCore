using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities;
using SabersCore.Models;

namespace SabersCore.Services;

internal class SaberMetadataCache : ISaberMetadataSnapshotCache
{
    private readonly Dictionary<string, CustomSaberMetadata> cache = [];

    public bool TryAdd(CustomSaberMetadata saberMetadata) =>
        cache.TryAdd(saberMetadata.SaberFile.Hash, saberMetadata);

    public void Remove(string saberHash) =>
        cache.Remove(saberHash);

    public bool TryGetMetadata(string? saberHash, [NotNullWhen(true)] out CustomSaberMetadata? meta) => 
        (meta = GetOrDefault(saberHash)) != null;

    public CustomSaberMetadata? GetOrDefault(string? saberHash)
    {
        if (saberHash is null || !cache.TryGetValue(saberHash, out var meta))
        {
            return null;
        }
        
        meta.SaberFile.FileInfo.Refresh();
        
        if (!meta.SaberFile.FileInfo.Exists)
        {
            Remove(saberHash);
            return null;
        }
        
        return meta;
    }

    public void Clear() => 
        cache.Clear();

    public IEnumerable<CustomSaberMetadata> GetRefreshedMetadata()
    {
        foreach (var meta in cache.Values)
        {
            meta.SaberFile.FileInfo.Refresh();
            yield return meta;
        }
    }

    public async Task<CustomSaberMetadata[]> GetRefreshedMetadataAsync(CancellationToken token)
    {
        await UnityGame.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();
        var metadata = cache.Values.ToArray();
        var paths = metadata.Select(meta => meta.SaberFile.FileInfo.FullName).ToArray();
        var files = await Task.Factory.StartNew(RefreshFiles, (paths, token), token,
            TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        await UnityGame.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();

        for (var i = 0; i < metadata.Length; i++)
            metadata[i] = metadata[i] with { SaberFile = metadata[i].SaberFile with { FileInfo = files[i] } };
        return metadata;
    }

    private static FileInfo[] RefreshFiles(object state)
    {
        var (paths, token) = ((string[], CancellationToken))state;
        var files = new FileInfo[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            files[i] = new FileInfo(paths[i]);
            files[i].Refresh();
        }
        return files;
    }
}
