using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SabersCore.Models;

namespace SabersCore.Services;

/// <summary>
/// Class for loading different kinds of custom saber assets
/// </summary>
internal class CustomSabersLoader : ISabersLoader
{
    private readonly IPrefabCache prefabCache;
    private readonly SaberLoader saberLoader;
    private readonly Saber2Loader saber2Loader;

    public CustomSabersLoader(
        IPrefabCache prefabCache, SaberLoader saberLoader, Saber2Loader saber2Loader)
    {
        this.prefabCache = prefabCache;
        this.saberLoader = saberLoader;
        this.saber2Loader = saber2Loader;
    }

    private readonly Dictionary<SaberFileInfo, Task<ISaberData>> runningTasks = [];
    private readonly HashSet<SaberFileInfo> keepRequested = [];
    private readonly HashSet<SaberFileInfo> sharedLoads = [];

    public async Task<ISaberData> GetSaberData(SaberFileInfo saberFile, bool keepSaberInstance, CancellationToken token)
    {
        if (prefabCache.TryGetPrefab(saberFile.Hash, out var cachedSaberData))
        {
            return cachedSaberData;
        }
        
        if (runningTasks.TryGetValue(saberFile, out var task))
        {
            sharedLoads.Add(saberFile);
            if (keepSaberInstance) keepRequested.Add(saberFile);
            return await task;
        }

        try
        {
            if (keepSaberInstance) keepRequested.Add(saberFile);
            var loadSaberDataTask = LoadAndCacheAsync(saberFile);
            runningTasks.Add(saberFile, loadSaberDataTask);

            var saberData = await loadSaberDataTask;

            if (token.IsCancellationRequested)
            {
                // Another caller may still be using a shared load.
                if (!sharedLoads.Contains(saberFile)) saberData.Dispose();
                throw new OperationCanceledException();
            }

            return saberData;
        }
        catch (DirectoryNotFoundException)
        {
            return new NoSaberData(saberFile, SaberLoaderError.FileNotFound);
        }
        finally
        {
            runningTasks.Remove(saberFile);
            sharedLoads.Remove(saberFile);
        }
    }

    // Caches before any awaiting caller resumes, so no caller can dispose data another caller keeps.
    private async Task<ISaberData> LoadAndCacheAsync(SaberFileInfo saberFile)
    {
        try
        {
            var saberData = await LoadSaberDataAsync(saberFile);
            if (keepRequested.Contains(saberFile)) prefabCache.AddPrefab(saberData);
            return saberData;
        }
        finally
        {
            keepRequested.Remove(saberFile);
        }
    }

    private async Task<ISaberData> LoadSaberDataAsync(SaberFileInfo saberFile) => saberFile.FileInfo.Extension switch
    {
        ".saber" => await saberLoader.LoadCustomSaberAsync(saberFile),
        ".saber2" => await saber2Loader.LoadSaber2Async(saberFile),
        _ => new NoSaberData(saberFile, SaberLoaderError.InvalidFileType)
    };
}
