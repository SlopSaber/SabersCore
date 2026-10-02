using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AssetComponents.Models;
using IPA.Utilities;
using SabersCore.Models;
using SabersCore.Utilities.Common;
using SabersCore.Utilities.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SabersCore.Services;

internal class Saber2Loader : IDisposable
{
    private readonly SpriteCache spriteCache;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim archiveGate = new(1, 1);
    private Task<SaberArchiveIO.ReadResult>? archiveReadTask;
    private bool disposed;

    public Saber2Loader(SpriteCache spriteCache)
    {
        this.spriteCache = spriteCache;
    }

    /// <summary>
    /// Loads a custom saber from a .saber2 file
    /// </summary>
    public async Task<ISaberData> LoadSaber2Async(SaberFileInfo saberFile)
    {
        await UnityGame.SwitchToMainThreadAsync();
        if (disposed) throw new OperationCanceledException();
        var token = lifetime.Token;
        AssetBundle? bundle = null;
        GameObject? saberPrefab = null;
        SaberArchiveIO.ReadResult? read = null;

        try
        {
            await archiveGate.WaitAsync(token);
            await UnityGame.SwitchToMainThreadAsync();
            try
            {
                token.ThrowIfCancellationRequested();
                archiveReadTask = Task.Factory.StartNew(SaberArchiveIO.Read, (saberFile.FileInfo.FullName, token),
                    token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                read = await archiveReadTask;
                await UnityGame.SwitchToMainThreadAsync();
            }
            finally
            {
                await UnityGame.SwitchToMainThreadAsync();
                archiveReadTask = null;
                archiveGate.Release();
            }
            token.ThrowIfCancellationRequested();
            if (!read.FileExists)
            {
                return new NoSaberData(saberFile, SaberLoaderError.FileNotFound);
            }

            Plugin.Log.Debug($"Attempting to load saber2 file - {saberFile.FileInfo.Name}");

            if (read.ReadError != null) ExceptionDispatchInfo.Capture(read.ReadError).Throw();
            if (read.Error != SaberLoaderError.None)
            {
                return new NoSaberData(saberFile, read.Error);
            }

            var saber2 = read.Model!;
            bundle = await BundleLoading.LoadBundle(read.Bundle!);
            read.Bundle = null;
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            if (bundle == null)
            {
                return new NoSaberData(saberFile, SaberLoaderError.NullBundle);
            }

            saberPrefab = await BundleLoading.LoadAsset<GameObject>(bundle, "_CustomSaber");
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            if (saberPrefab == null)
                saberPrefab = await BundleLoading.LoadAsset<GameObject>(bundle, AssetBundleDefinition.SaberAssetName);
            await UnityGame.SwitchToMainThreadAsync();
            token.ThrowIfCancellationRequested();
            if (saberPrefab == null)
            {
                bundle.Unload(true);
                return new NoSaberData(saberFile, SaberLoaderError.NullAsset);
            }

            saberPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            saberPrefab.name += $" {saber2.ModelName}";

            if (read.IconError != null) ExceptionDispatchInfo.Capture(read.IconError).Throw();
            var icon = GetDownscaledIcon(read.Icon, saber2.ModelName);
            read.Icon = null;
            spriteCache.AddSprite(saberFile.Hash, icon);

            var saberName = RichTextString.Create(saber2.ModelName);
            var authorName = RichTextString.Create(saber2.AuthorName);
            var saberIcon = PluginResources.NullCoverImage;
            var descriptor = new Descriptor(saberName, authorName, saberIcon);
            var hasTrails = CustomTrailUtils.GetTrailsFromCustomSaber(saberPrefab).Any();
            var metadata = new CustomSaberMetadata(saberFile, SaberLoaderError.None, descriptor, hasTrails);
            var saber2Prefab = new CustomSaberPrefab(saberPrefab);
            return new CustomSaberData(metadata, saber2Prefab);
        }
        catch (Exception ex)
        {
            await UnityGame.SwitchToMainThreadAsync();
            if (bundle != null) bundle.Unload(true);
            if (disposed) throw new OperationCanceledException();
            Plugin.Log.Error($"Encountered a problem while trying to load file - {saberFile.FileInfo.Name}\n{ex}");
            return new NoSaberData(saberFile, SaberLoaderError.Unknown);
        }
        finally
        {
            if (read != null)
            {
                read.Bundle = null;
                read.Icon = null;
            }
            await UnityGame.SwitchToMainThreadAsync();
            if (saberPrefab != null) saberPrefab.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            if (bundle != null) bundle.Unload(false);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        try
        {
            archiveReadTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { }
        finally
        {
            lifetime.Dispose();
        }
    }

    private static Sprite? GetDownscaledIcon(byte[]? image, string name)
    {
        if (image == null) return null;
        var icon = new Texture2D(2, 2).ToSprite(image);
        if (icon == null)
        {
            return null;
        }
        if (icon.texture == null)
        {
            Object.Destroy(icon);
            return null;
        }
        var downscaledIcon = icon.texture.Downscale(128, 128).ToSprite(rename: name);
        Object.Destroy(icon);
        return downscaledIcon;
    }
}
