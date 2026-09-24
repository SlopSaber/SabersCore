using System.IO;
using System.Threading.Tasks;
using AssetBundleLoadingTools.Utilities;
using UnityEngine;

namespace SabersCore.Utilities.Common;

/// <summary>
/// Uses AssetBundleExtensions to greatly simplify async <seealso cref="AssetBundle"/> loading
/// </summary>
internal static class BundleLoading
{
    public static async Task<AssetBundle?> LoadBundle(string path) =>
        await AssetBundleExtensions.LoadFromFileAsync(path);

    public static async Task<AssetBundle?> LoadBundle(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return await AssetBundleExtensions.LoadFromMemoryAsync(memoryStream.ToArray());
    }

    public static async Task<T?> LoadAsset<T>(AssetBundle bundle, string assetPath) where T : Object =>
        await AssetBundleExtensions.LoadAssetAsync<T>(bundle, assetPath);
}
