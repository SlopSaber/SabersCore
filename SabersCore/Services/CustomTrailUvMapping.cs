using System.Collections.Generic;
using SiraUtil.Affinity;

namespace SabersCore.Services;

internal class CustomTrailUvMapping : IAffinity
{
    private readonly HashSet<SaberTrailRenderer> renderers = [];

    internal void Register(SaberTrailRenderer renderer)
    {
        renderers.RemoveWhere(value => value == null);
        renderers.Add(renderer);
    }

    [AffinityPostfix]
    [AffinityPatch(typeof(SaberTrailRenderer), nameof(SaberTrailRenderer.UpdateVertices))]
    private void RestoreAuthoredAgeDirection(SaberTrailRenderer __instance)
    {
        if (!renderers.Contains(__instance)) return;

        // Custom-trail materials use V=1 at the blade; the native renderer writes age from zero.
        var uvs = __instance._uvs;
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i].y = 1f - uvs[i].y;
        }
    }
}
