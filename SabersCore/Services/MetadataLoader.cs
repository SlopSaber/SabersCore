using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using IPA.Utilities;
using SabersCore.Models;
using SabersCore.Utilities.Common;
using SabersCore.Utilities.Extensions;
using SiraUtil.Zenject;
using UnityEngine;

namespace SabersCore.Services;

internal class MetadataLoader : IAsyncInitializable, IDisposable, ISaberMetadataLoader
{
    private readonly ISabersLoader sabersLoader;
    private readonly SpriteCache spriteCache;
    private readonly IPrefabCache prefabCache;
    private readonly SaberMetadataCacheMigrationManager saberMetadataCacheMigrationManager;
    private readonly ISaberFileManager saberFileManager;
    private readonly ISaberMetadataCache saberMetadataCache;
    private readonly DirectoryManager directoryManager;
    private readonly SaberMetadataConverter saberMetadataConverter;

    public MetadataLoader(ISabersLoader sabersLoader,
        SpriteCache spriteCache,
        IPrefabCache prefabCache,
        SaberMetadataCacheMigrationManager saberMetadataCacheMigrationManager,
        ISaberFileManager saberFileManager, 
        ISaberMetadataCache saberMetadataCache, 
        DirectoryManager directoryManager,
        SaberMetadataConverter saberMetadataConverter)
    {
        this.sabersLoader = sabersLoader;
        this.spriteCache = spriteCache;
        this.prefabCache = prefabCache;
        this.saberMetadataCacheMigrationManager = saberMetadataCacheMigrationManager;
        this.saberFileManager = saberFileManager;
        this.saberMetadataCache = saberMetadataCache;
        this.directoryManager = directoryManager;
        this.saberMetadataConverter = saberMetadataConverter;
    }

    private CancellationTokenSource reloadTokenSource = new();
    private MetadataLoaderProgress currentProgress = new(string.Empty);
    private readonly SemaphoreSlim cacheSaveGate = new(1, 1);
    private Task? cacheFileTask;
    private string? cacheTemporaryPath;
    private bool disposed;

    private string CacheArchiveFilePath => Path.Combine(directoryManager.UserData.FullName, "cache");

    public event Action<MetadataLoaderProgress>? LoadingProgressChanged;
    
    public MetadataLoaderProgress CurrentProgress
    {
        get => currentProgress;
        private set
        {
            if (disposed || currentProgress == value) return;
            currentProgress = value;
            LoadingProgressChanged?.Invoke(value);
        }
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        await UnityGame.SwitchToMainThreadAsync();
        if (disposed) return;
        reloadTokenSource.CancelThenDispose();
        reloadTokenSource = new();

        try
        {
            await ReloadAsync(reloadTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (!disposed) Plugin.Log.Debug("Reload operation cancelled.");
        }
        catch (Exception e)
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (!disposed) Plugin.Log.Error($"Problem encountered during reload\n{e}");
        }
    }
    
    private async Task ReloadAsync(CancellationToken token) 
    {
        prefabCache.Clear();
        saberMetadataCache.Clear();
        var stopwatch = Stopwatch.StartNew();
        var simpleIntProgress = new Progress<int>(v =>
        {
            if (!token.IsCancellationRequested) CurrentProgress = currentProgress with { StagePercent = v };
        });
        IProgress<MetadataLoaderProgress> stageChangedProgress = new Progress<MetadataLoaderProgress>(p =>
        {
            if (!token.IsCancellationRequested) CurrentProgress = p;
        });
        
        stageChangedProgress.Report(new("Retrieving Saber Files"));
        var localSaberFiles = await saberFileManager.ReloadAllSaberFiles(token, simpleIntProgress);
        await UnityGame.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();

        stageChangedProgress.Report(new("Loading Cache"));
        var localCacheFile = await GetLocalCache(token);

        var sabersToLoad = await GetSabersToLoad(localCacheFile, localSaberFiles, token);
        await UnityGame.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();
        Plugin.Log.Notice($"Found {sabersToLoad.Length} saber files to load");
        
        stageChangedProgress.Report(new("Loading Sabers"));
        var updatedLocalCache = await GetUpdatedLocalCache(localCacheFile, sabersToLoad, token, simpleIntProgress);
        
        if (localCacheFile != updatedLocalCache)
        {
            stageChangedProgress.Report(new("Saving Metadata"));
            await SaveMetadataToLocalCache(updatedLocalCache, token);
        }

        await UpdateMetadataCache(updatedLocalCache, localSaberFiles, token);
        await UnityGame.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();

        stopwatch.Stop();
        Plugin.Log.Notice($"Cache loading took {stopwatch.ElapsedMilliseconds}ms");

        stageChangedProgress.Report(new("Completed", Completed: true));
    }

    private async Task<CacheFileModel> GetLocalCache(CancellationToken token)
    {
        var migrated = await saberMetadataCacheMigrationManager.MigrationTask;
        await UnityGame.SwitchToMainThreadAsync();
        token.ThrowIfCancellationRequested();
        if (!migrated)
        {
            Plugin.Log.Warn("Internal reload was denied because of a failure during cache migration");
            return CacheFileModel.Empty;
        }
        token.ThrowIfCancellationRequested();

        await cacheSaveGate.WaitAsync(token);
        await UnityGame.SwitchToMainThreadAsync();
        try
        {
            token.ThrowIfCancellationRequested();
            var read = await RunCacheIO(MetadataCacheIO.Read, (CacheArchiveFilePath, token), token);
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            var cache = read.Metadata ?? CacheFileModel.Empty;
            try
            {
                for (var i = 0; i < cache.CachedMetadata.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (read.ImageErrors[i] != null)
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(read.ImageErrors[i]!).Throw();
                    var meta = cache.CachedMetadata[i];
                    var image = read.Images[i];
                    read.Images[i] = null;
                    var sprite = image == null ? null : new Texture2D(2, 2).ToSprite(image, rename: meta.SaberName);
                    spriteCache.AddSprite(meta.Hash, sprite);
                }
            }
            finally
            {
                Array.Clear(read.Images, 0, read.Images.Length);
                Array.Clear(read.ImageErrors, 0, read.ImageErrors.Length);
            }
            return cache;
        }
        finally
        {
            cacheSaveGate.Release();
        }
    }

    private async Task SaveMetadataToLocalCache(CacheFileModel cacheFile, CancellationToken token)
    {
        await cacheSaveGate.WaitAsync(token);
        await UnityGame.SwitchToMainThreadAsync();
        try
        {
            token.ThrowIfCancellationRequested();
            var temporaryPath = Path.Combine(directoryManager.UserData.FullName, "temp");
            cacheTemporaryPath = temporaryPath;
            await RunCacheIO(MetadataCacheIO.CreateTemporaryDirectories, temporaryPath);
            await UnityGame.SwitchToMainThreadAsync();
            try
            {
                foreach (var meta in cacheFile.CachedMetadata)
                {
                    if (disposed || token.IsCancellationRequested) return;
                    var image = spriteCache.GetSprite(meta.Hash)?.texture.EncodeToPNG();
                    if (image == null) continue;
                    var imagePath = Path.Combine(temporaryPath, "images", meta.Hash + ".png");
                    await RunCacheIO(MetadataCacheIO.WriteImage, (imagePath, image));
                    await UnityGame.SwitchToMainThreadAsync();
                }

                if (disposed || token.IsCancellationRequested) return;
                var useWorkerJson = JsonConvert.DefaultSettings == null && cacheFile.GetType() == typeof(CacheFileModel);
                var json = useWorkerJson ? null : JsonConvert.SerializeObject(cacheFile, Formatting.None);
                if (disposed || token.IsCancellationRequested) return;
                var capturedMetadata = useWorkerJson
                    ? new CacheFileModel(cacheFile.Version, cacheFile.CachedMetadata.ToArray()) : null;
                await RunCacheIO(MetadataCacheIO.SaveArchive, (temporaryPath, CacheArchiveFilePath, capturedMetadata, json));
                await UnityGame.SwitchToMainThreadAsync();
            }
            catch (Exception ex)
            {
                await UnityGame.SwitchToMainThreadAsync();
                if (!disposed) Plugin.Log.Warn($"Problem encountered when saving the saber metadata cache\n{ex}");
            }
            finally
            {
                if (cacheTemporaryPath != null)
                {
                    try
                    {
                        await RunCacheIO(MetadataCacheIO.DeleteTemporaryDirectory, temporaryPath);
                        await UnityGame.SwitchToMainThreadAsync();
                    }
                    finally
                    {
                        cacheTemporaryPath = null;
                    }
                }
            }
        }
        finally
        {
            cacheSaveGate.Release();
        }
    }

    private async Task RunCacheIO(Action<object> action, object state)
    {
        var task = Task.Factory.StartNew(action, state, CancellationToken.None,
            TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        cacheFileTask = task;
        try
        {
            await task;
        }
        finally
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (ReferenceEquals(cacheFileTask, task)) cacheFileTask = null;
        }
    }

    private async Task<T> RunCacheIO<T>(Func<object, T> action, object state, CancellationToken token)
    {
        var task = Task.Factory.StartNew(action, state, token,
            TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        cacheFileTask = task;
        try
        {
            return await task;
        }
        finally
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (ReferenceEquals(cacheFileTask, task)) cacheFileTask = null;
        }
    }
    
    private async Task<CacheFileModel> GetUpdatedLocalCache(
        CacheFileModel existingCache, SaberFileInfo[] sabersToLoad, CancellationToken token, IProgress<int> progress)
    {
        
        if (!sabersToLoad.Any())
        {
            return existingCache; // nothing to update
        }

        var loadedMetadata = await LoadMetadataFromSabers(sabersToLoad, token, progress);
        
        if (!loadedMetadata.Any())
        {
            return existingCache;
        }
        
        // add the new metadata to the existing metadata
        var cachedMetadata = existingCache.CachedMetadata.Concat(loadedMetadata).ToArray();
        
        return new(Plugin.Metadata.HVersion.ToString(), cachedMetadata);
    }
    
    private async Task<List<SaberMetadataModel>> LoadMetadataFromSabers(
        IList<SaberFileInfo> sabersForCaching, CancellationToken token, IProgress<int> progress)
    {
        progress.Report(0);
        var loadedSaberMetadata = new List<SaberMetadataModel>();
        int lastPercent = 0;

        for (var i = 0; i < sabersForCaching.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            using var saberData = await sabersLoader.GetSaberData(sabersForCaching[i], false, token);
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();

            loadedSaberMetadata.Add(saberMetadataConverter.CreateJson(saberData.Metadata));

            int newPercent = (i + 1) * 100 / sabersForCaching.Count;
            if (newPercent != lastPercent)
            {
                progress.Report(newPercent);
                lastPercent = newPercent;
            }
        }

        return loadedSaberMetadata;
    }

    private async Task UpdateMetadataCache(
        CacheFileModel updatedLocalCache, SaberFileInfo[] localSaberFiles, CancellationToken token)
    {
        await cacheSaveGate.WaitAsync(token);
        await UnityGame.SwitchToMainThreadAsync();
        try
        {
            token.ThrowIfCancellationRequested();
            var hashes = localSaberFiles.Select(file => file.Hash).ToArray();
            var prepared = await RunCacheIO(MetadataCacheIO.PrepareMetadata,
                (updatedLocalCache.CachedMetadata.ToArray(), hashes, token), token);
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            foreach (var row in prepared)
            {
                var saberMetadata = saberMetadataConverter.ConvertJson(row.Metadata, localSaberFiles[row.FileIndex],
                    row.SaberName, row.AuthorName);
                saberMetadataCache.TryAdd(saberMetadata);
            }
        }
        finally
        {
            cacheSaveGate.Release();
        }
    }

    private async Task<SaberFileInfo[]> GetSabersToLoad(
        CacheFileModel existingCache, SaberFileInfo[] localSaberFiles, CancellationToken token)
    {
        await cacheSaveGate.WaitAsync(token);
        await UnityGame.SwitchToMainThreadAsync();
        try
        {
            token.ThrowIfCancellationRequested();
            var hashes = localSaberFiles.Select(file => file.Hash).ToArray();
            var indices = await RunCacheIO(MetadataCacheIO.SelectUncachedFiles,
                (existingCache.CachedMetadata.ToArray(), hashes, token), token);
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            return indices.Select(index => localSaberFiles[index]).ToArray();
        }
        finally
        {
            cacheSaveGate.Release();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        reloadTokenSource.CancelThenDispose();
        try
        {
            cacheFileTask?.GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            Plugin.Log.Warn($"Problem encountered when saving the saber metadata cache\n{error}");
        }
        finally
        {
            cacheFileTask = null;
            if (cacheTemporaryPath != null)
            {
                try
                {
                    Task.Factory.StartNew(MetadataCacheIO.DeleteTemporaryDirectoryIfPresent, cacheTemporaryPath,
                        CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                        .GetAwaiter().GetResult();
                }
                catch (Exception error)
                {
                    Plugin.Log.Warn($"Problem encountered when saving the saber metadata cache\n{error}");
                }
                finally
                {
                    cacheTemporaryPath = null;
                }
            }
        }
    }
}
