using System;
using System.Collections.Generic;
using System.Linq;
using SaberComponents.Components;
using UnityEngine;

namespace SabersCore.Models;

internal static class ElectroSaberMaterialAdapter
{
    public static Material[] Apply(GameObject saber)
    {
        var renderers = saber.GetComponentsInChildren<MeshRenderer>(true);
        if (!renderers.Any(renderer => renderer.sharedMaterials.Any(IsElectroMaterial)))
            return [];

        var gameHandle = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(material =>
            material != null && material.name == "Handle" &&
            material.shader != null && material.shader.name == "Custom/SimpleLit");
        if (gameHandle == null)
        {
            Plugin.Log.Warn("Electro Saber: Beat Saber Handle material is unavailable");
            return [];
        }

        var replacements = new Dictionary<Material, Material>();
        var convertedRenderers = 0;
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            var converted = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!IsElectroMaterial(source)) continue;
                if (!replacements.TryGetValue(source, out var replacement))
                {
                    replacement = CreateReplacement(gameHandle, source);
                    replacements.Add(source, replacement);
                }
                materials[i] = replacement;
                converted = true;
            }
            if (!converted) continue;

            renderer.sharedMaterials = materials;
            var colorer = renderer.GetComponent<MaterialColorer>();
            if (colorer != null)
            {
                colorer.propertyName = "_Color";
                colorer.multiplierColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            }
            convertedRenderers++;
        }

        Plugin.Log.Notice($"Electro Saber: converted {convertedRenderers} mesh renderers to game Handle shader ({replacements.Count} materials)");
        return replacements.Values.ToArray();
    }

    private static bool IsElectroMaterial(Material material)
    {
        var name = material?.shader?.name;
        return name == "ElectroSaber/Body" || name == "ElectroSaber/Glow";
    }

    private static Material CreateReplacement(Material gameHandle, Material source)
    {
        var replacement = new Material(gameHandle) { name = source.name + " (game shader)" };
        var sourceColor = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.gray;
        var tint = new Color(
            0.11f + 0.28f * Mathf.Clamp01(sourceColor.r),
            0.11f + 0.28f * Mathf.Clamp01(sourceColor.g),
            0.11f + 0.28f * Mathf.Clamp01(sourceColor.b),
            1f);
        replacement.SetColor("_Color", tint);
        replacement.SetColor("_TintColor", tint);
        replacement.SetColor("_AddColor", Color.clear);
        replacement.SetColor("_EmissionColor", Color.black);

        if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") is { } texture)
        {
            replacement.SetTexture("_DiffuseTexture", texture);
            replacement.SetTextureScale("_DiffuseTexture", source.GetTextureScale("_MainTex"));
            replacement.SetTextureOffset("_DiffuseTexture", source.GetTextureOffset("_MainTex"));
            replacement.SetFloat("_EnableDiffuseTexture", 1f);
        }
        return replacement;
    }
}
