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
        return name == "ElectroSaber/Body" || name == "ElectroSaber/Glow" || name == "ElectroSaber/Trail";
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
        replacement.SetColor("_RimLightColor", Color.black);
        replacement.SetFloat("_RimLight", 0f);
        replacement.SetFloat("_RimLightIntensity", 0f);
        replacement.SetFloat("_RimLightBloomIntensity", 0f);
        replacement.DisableKeyword("_RIMLIGHT_LERP");
        replacement.DisableKeyword("_RIMLIGHT_ADDITIVE");

        var goldHardware = source.name.IndexOf("Gold", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source.name.IndexOf("Copper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source.name.StartsWith("OkaFresnelThinGreeen", StringComparison.Ordinal);
        if (goldHardware)
        {
            var goldTint = new Color(
                0.13f + 0.4f * Mathf.Clamp01(sourceColor.r),
                0.05f + 0.34f * Mathf.Clamp01(sourceColor.g),
                0.02f + 0.2f * Mathf.Clamp01(sourceColor.b),
                1f);
            replacement.SetColor("_Color", goldTint);
            replacement.SetColor("_TintColor", goldTint);
        }

        var usesSaberColor = source.HasProperty("_UseSaberColor") && source.GetFloat("_UseSaberColor") > 0.5f;
        var metalHardware = !usesSaberColor && (goldHardware ||
            source.name.IndexOf("MatCap", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source.name.StartsWith("Material", StringComparison.Ordinal));
        if (metalHardware)
        {
            replacement.SetColor("_RimLightColor", goldHardware
                ? new Color(0.85f, 0.65f, 0.25f, 1f)
                : new Color(0.38f, 0.38f, 0.38f, 1f));
            replacement.SetFloat("_RimLight", 1f);
            replacement.SetFloat("_RimLightIntensity", goldHardware ? 0.7f : 0.45f);
            replacement.SetFloat("_RimLightBloomIntensity", goldHardware ? 1f : 0.65f);
            replacement.EnableKeyword("_RIMLIGHT_LERP");
            replacement.SetFloat("_ReflectionIntensity", 0.75f);
            replacement.SetFloat("_ReflectionProbeIntensity", 0.75f);
            replacement.SetFloat("_SpecularIntensity", 0.75f);
            replacement.SetFloat("_Smoothness", 0.75f);
            replacement.SetFloat("_Glossiness", 0.5f);
        }
        if (usesSaberColor)
        {
            var glow = source.HasProperty("_Glow") ? source.GetFloat("_Glow") : 1f;
            replacement.SetFloat("_RimLight", 1f);
            replacement.SetFloat("_RimLightIntensity", Mathf.Clamp(0.5f + glow * 0.5f, 0.6f, 1f));
            replacement.SetFloat("_RimLightBloomIntensity", Mathf.Clamp(glow * 1.5f, 0.8f, 1.6f));
            replacement.EnableKeyword("_RIMLIGHT_LERP");
            if (source.name.StartsWith("DeepRed", StringComparison.Ordinal) ||
                source.name.StartsWith("DeepBlue", StringComparison.Ordinal))
            {
                replacement.SetFloat("_ReflectionIntensity", 0.85f);
                replacement.SetFloat("_ReflectionProbeIntensity", 0.85f);
                replacement.SetFloat("_SpecularIntensity", 0.85f);
                replacement.SetFloat("_Smoothness", 0.75f);
            }
        }

        if (!goldHardware && source.HasProperty("_MainTex") && source.GetTexture("_MainTex") is { } texture)
        {
            replacement.SetTexture("_DiffuseTexture", texture);
            replacement.SetTextureScale("_DiffuseTexture", source.GetTextureScale("_MainTex"));
            replacement.SetTextureOffset("_DiffuseTexture", source.GetTextureOffset("_MainTex"));
            replacement.SetFloat("_EnableDiffuseTexture", 1f);
        }
        return replacement;
    }
}
