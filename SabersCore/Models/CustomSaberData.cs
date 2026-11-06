namespace SabersCore.Models;

public class CustomSaberData(
    CustomSaberMetadata metadata,
    ISaberPrefab customSaberPrefab) : ISaberData
{
    public CustomSaberMetadata Metadata { get; } = metadata;
    public ISaberPrefab Prefab { get; } = customSaberPrefab;

    // Cached data is owned by the prefab cache; other holders' Dispose calls must not destroy it.
    internal bool IsCached { get; set; }

    public void Dispose()
    {
        if (IsCached) return;
        Prefab.Dispose();
    }
}