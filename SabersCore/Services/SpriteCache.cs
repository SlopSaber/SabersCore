using System.Collections.Generic;
using UnityEngine;

namespace SabersCore.Services;

internal class SpriteCache
{
    private readonly Dictionary<string, Sprite> cache = [];

    public void AddSprite(string saberHash, Sprite? sprite)
    {
        if (sprite == null) return;
        if (cache.TryAdd(saberHash, sprite)) return;

        Object.Destroy(sprite.texture);
        Object.Destroy(sprite);
    }

    public Sprite? GetSprite(string relativePath) => cache.GetValueOrDefault(relativePath);
}
