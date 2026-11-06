using System.Collections.Generic;
using System.Linq;
using SiraUtil.Affinity;
using UnityEngine;

namespace SabersCore.Services;

internal class CustomTrailUvMapping : IAffinity
{
    private readonly Dictionary<SaberTrailRenderer, bool> renderers = [];

    internal void Register(SaberTrailRenderer renderer, bool flipAgeDirection)
    {
        foreach (var destroyed in renderers.Keys.Where(value => value == null).ToArray())
        {
            renderers.Remove(destroyed);
        }

        renderers[renderer] = flipAgeDirection;
    }

    [AffinityPostfix]
    [AffinityPatch(typeof(SaberTrailRenderer), nameof(SaberTrailRenderer.UpdateVertices))]
    private void MapAgeCoordinate(SaberTrailRenderer __instance)
    {
        if (!renderers.TryGetValue(__instance, out var flip)) return;

        // The trail keeps a few samples past its duration, so age can exceed 1. Custom materials may
        // use repeating textures, which would wrap that tail back to the blade end of the texture.
        // Custom-trail SDK materials also use V=1 at the blade; the native renderer writes age from zero.
        var uvs = __instance._uvs;
        for (int i = 0; i < uvs.Length; i++)
        {
            var age = Mathf.Clamp01(uvs[i].y);
            uvs[i].y = flip ? 1f - age : age;
        }
    }
}
