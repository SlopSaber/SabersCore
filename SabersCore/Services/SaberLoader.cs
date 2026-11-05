using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SaberComponents.Components;
using SabersCore.Models;
using SabersCore.Utilities.Common;
using SabersCore.Utilities.Extensions;
using UnityEngine;

namespace SabersCore.Services;

internal class SaberLoader
{
    private readonly SpriteCache spriteCache;

    public SaberLoader(SpriteCache spriteCache)
    {
        this.spriteCache = spriteCache;
    }

    /// <summary>
    /// Loads a custom saber from a .saber file
    /// </summary>
    public async Task<ISaberData> LoadCustomSaberAsync(SaberFileInfo saberFile)
    {
        AssetBundle? bundle = null;
        GameObject? saberPrefab = null;
        
        try
        {
            if (!saberFile.FileInfo.Exists)
            {
                return new NoSaberData(saberFile, SaberLoaderError.FileNotFound);
            }

            Plugin.Log.Debug($"Attempting to load saber file - {saberFile.FileInfo.Name}");

            LegacySaberConverter.EnsureLoaded();
            bundle = await BundleLoading.LoadBundle(saberFile.FileInfo.FullName);

            if (bundle == null)
            {
                return new NoSaberData(saberFile, SaberLoaderError.NullBundle);
            }

            saberPrefab = await BundleLoading.LoadAsset<GameObject>(bundle, "_CustomSaber");

            if (saberPrefab == null)
            {
                bundle.Unload(true);
                return new NoSaberData(saberFile, SaberLoaderError.NullAsset);
            }

            LegacySaberConverter.Convert(saberPrefab);
            AssetBundleLoadingTools.Rendering.LegacyRendering.Register(saberPrefab);
            var saberDescriptor = saberPrefab.GetComponent<SaberDescriptor>();
            var displayName = saberDescriptor != null ? saberDescriptor.SaberName : Path.GetFileNameWithoutExtension(saberFile.FileInfo.Name);
            var author = saberDescriptor != null ? saberDescriptor.AuthorName : string.Empty;
            saberPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            saberPrefab.name += $" {displayName}";

            Sprite? icon = null;
            if (saberDescriptor != null && saberDescriptor.CoverImage != null)
            {
                icon = saberDescriptor.CoverImage.DuplicateTexture().Downscale(128, 128).ToSprite(rename: displayName);
            }

            spriteCache.AddSprite(saberFile.Hash, icon);

            var saberName = RichTextString.Create(displayName);
            var authorName = RichTextString.Create(author);
            var saberIcon = icon != null ? icon : PluginResources.NullCoverImage;
            var descriptor = new Descriptor(saberName, authorName, saberIcon);
            var hasTrails = CustomTrailUtils.GetTrailsFromCustomSaber(saberPrefab).Any();
            var metadata = new CustomSaberMetadata(saberFile, SaberLoaderError.None, descriptor, hasTrails); 
            var customSaberPrefab = new CustomSaberPrefab(saberPrefab);
            return new CustomSaberData(metadata, customSaberPrefab);
        }
        catch (Exception ex)
        {
            if (bundle != null) bundle.Unload(true);
            bundle = null;
            Plugin.Log.Error($"Encountered a problem while trying to load file - {saberFile.FileInfo.Name}\n{ex}");
            return new NoSaberData(saberFile, SaberLoaderError.Unknown);
        }
        finally
        {
            if (saberPrefab != null) saberPrefab.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            if (bundle != null) bundle.Unload(false);
        }
    }
}
