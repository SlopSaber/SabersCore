using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IPA.Utilities;
using SabersCore.Models;

namespace SabersCore.Services;

/// <summary>
/// Class for loading different kinds of custom saber assets
/// </summary>
internal class CustomSabersLoader : ISabersLoader, IDisposable
{
    private readonly IPrefabCache prefabCache;
    private readonly Saber2Loader saber2Loader;

    public CustomSabersLoader(
        IPrefabCache prefabCache, Saber2Loader saber2Loader)
    {
        this.prefabCache = prefabCache;
        this.saber2Loader = saber2Loader;
    }

    private readonly Dictionary<SaberFileInfo, Task<ISaberData>> runningTasks = [];
    private bool disposed;

    public async Task<ISaberData> GetSaberData(SaberFileInfo saberFile, bool keepSaberInstance, CancellationToken token)
    {
        await UnityGame.SwitchToMainThreadAsync();
        if (disposed) throw new OperationCanceledException();
        if (prefabCache.TryGetPrefab(saberFile.Hash, out var cachedSaberData))
        {
            return cachedSaberData;
        }
        
        if (runningTasks.TryGetValue(saberFile, out var task))
        {
            var existingSaberData = await task;
            await UnityGame.SwitchToMainThreadAsync();
            if (disposed) throw new OperationCanceledException();
            return existingSaberData;
        }

        try
        {
            var loadSaberDataTask = LoadSaberDataAsync(saberFile);
            runningTasks.Add(saberFile, loadSaberDataTask);

            var saberData = await loadSaberDataTask;
            await UnityGame.SwitchToMainThreadAsync();

            if (disposed || token.IsCancellationRequested)
            {
                saberData.Dispose();
                throw new OperationCanceledException();
            }

            if (keepSaberInstance)
            {
                prefabCache.AddPrefab(saberData);
            }

            return saberData;
        }
        catch (DirectoryNotFoundException)
        {
            return new NoSaberData(saberFile, SaberLoaderError.FileNotFound);
        }
        finally
        {
            await UnityGame.SwitchToMainThreadAsync();
            runningTasks.Remove(saberFile);
        }
    }

    public void Dispose() => disposed = true;

    private async Task<ISaberData> LoadSaberDataAsync(SaberFileInfo saberFile) => saberFile.FileInfo.Extension switch
    {
        ".saber2" => await saber2Loader.LoadSaber2Async(saberFile),
        _ => new NoSaberData(saberFile, SaberLoaderError.InvalidFileType)
    };
}
