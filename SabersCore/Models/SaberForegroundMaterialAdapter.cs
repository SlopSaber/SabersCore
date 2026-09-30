using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabersCore.Models;

internal static class SaberForegroundMaterialAdapter
{
    internal static Material[] Apply(GameObject saber, Material[] existingOwnedMaterials)
    {
        // Older game profiles keep their original render path. Modern URP draws
        // transparent Saber-layer objects after its screen-displacement pass.
        if (LayerMask.NameToLayer("ScreenDisplacement") != 17)
            return existingOwnedMaterials;

        var owned = new List<Material>(existingOwnedMaterials);
        var ownedSet = new HashSet<Material>(existingOwnedMaterials);
        var replacements = new Dictionary<Material, Material>();
        foreach (var renderer in saber.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.renderQueue > (int)RenderQueue.GeometryLast)
                    continue;

                if (!ownedSet.Contains(source))
                {
                    if (!replacements.TryGetValue(source, out var replacement))
                    {
                        replacement = new Material(source)
                        {
                            name = source.name + " (saber foreground)"
                        };
                        replacements.Add(source, replacement);
                        owned.Add(replacement);
                        ownedSet.Add(replacement);
                    }
                    materials[i] = replacement;
                }

                // Change pass selection, retaining the original shader, blend,
                // and depth state. Sabers still respect scene geometry.
                materials[i].renderQueue = (int)RenderQueue.Transparent;
                changed = true;
            }
            if (changed)
                renderer.sharedMaterials = materials;
        }

        return owned.ToArray();
    }
}
