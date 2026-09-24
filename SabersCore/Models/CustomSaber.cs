using System;
using System.Linq;
using SaberComponents.Components;
using SaberComponents.Models;
using SabersCore.Utilities.Extensions;
using UnityEngine;

namespace SabersCore.Models;

/// <summary>
/// A wrapper around a custom saber object instance
/// </summary>
internal class CustomSaber : ISaber
{
    private readonly MaterialColorer[] allColorers;
    private readonly MaterialColorer[] saberColors;
    private readonly MaterialColorer[] boostColors;
    private readonly Material[] ownedMaterials;

    public GameObject GameObject { get; }
    public EventManager EventManager { get; }

    public CustomSaber(GameObject gameObject)
    {
        GameObject = gameObject;
        GameObject.SetLayerRecursively(12);
        ownedMaterials = ElectroSaberMaterialAdapter.Apply(gameObject);
        EventManager = gameObject.TryGetComponentOrAdd<EventManager>();
        // colorableMaterials = CustomTrailUtils.GetColorableSaberMaterials(gameObject).ToArray();
        allColorers = gameObject.GetComponentsInChildren<MaterialColorer>(true) ?? [];
        saberColors = allColorers.Where(colorer => colorer.UsesSaberColors()).ToArray();
        boostColors = allColorers.Where(colorer => colorer.UsesBoostColors()).ToArray();
    }

    public void SetColor(ColorScheme colorScheme)
    {
        foreach (var colorer in allColorers)
        {
            var color = colorScheme.GetColorByType(colorer.colorSchemeType);
            if (ApplyDirectSaberColor(colorer, color)) continue;
            colorer.materialPropertyBlock ??= new();
            colorer.meshRenderer.GetPropertyBlock(colorer.materialPropertyBlock);
            colorer.materialPropertyBlock.SetColor(colorer.propertyName, color * colorer.multiplierColor);
            if (ownedMaterials.Length > 0 && colorer.propertyName == "_Color")
                colorer.materialPropertyBlock.SetColor("_RimLightColor", color);
            colorer.meshRenderer.SetPropertyBlock(colorer.materialPropertyBlock);
        }
    }

    public void UpdateBoostColors(ColorScheme colorScheme, bool isBoostOn)
    {
        foreach (var colorer in boostColors)
        {
            var color = colorScheme.GetBoostColorByType(colorer.colorSchemeType, isBoostOn);
            colorer.materialPropertyBlock ??= new();
            colorer.meshRenderer.GetPropertyBlock(colorer.materialPropertyBlock);
            colorer.materialPropertyBlock.SetColor(colorer.propertyName, color * colorer.multiplierColor);
            colorer.meshRenderer.SetPropertyBlock(colorer.materialPropertyBlock);
        }
    }

    public void SetColor(Color color, SaberType saberType)
    {
        foreach (var colorer in saberColors)
        {
            colorer.materialPropertyBlock ??= new();
            colorer.meshRenderer.GetPropertyBlock(colorer.materialPropertyBlock);
            if ((saberType == SaberType.SaberA && colorer.colorSchemeType == ColorSchemeType.LeftSaber)
                || (saberType == SaberType.SaberB && colorer.colorSchemeType == ColorSchemeType.RightSaber))
            {
                if (ApplyDirectSaberColor(colorer, color)) continue;
                colorer.materialPropertyBlock.SetColor(colorer.propertyName, color * colorer.multiplierColor);
                if (ownedMaterials.Length > 0 && colorer.propertyName == "_Color")
                    colorer.materialPropertyBlock.SetColor("_RimLightColor", color);
                colorer.meshRenderer.SetPropertyBlock(colorer.materialPropertyBlock);
            }
        }
    }

    private static bool ApplyDirectSaberColor(MaterialColorer colorer, Color color)
    {
        var applied = false;
        foreach (var material in colorer.meshRenderer.sharedMaterials)
        {
            if (material == null || material.shader == null) continue;
            if (material.shader.name == "ElectroSaber/BladeBloom")
            {
                material.SetColor("_SaberColor", color * colorer.multiplierColor);
                applied = true;
            }
            else if (material.shader.name.StartsWith(".poiyomi/", StringComparison.OrdinalIgnoreCase) &&
                     material.HasProperty("_CustomColors") && material.GetFloat("_CustomColors") > 0.5f)
            {
                var saberColor = color * colorer.multiplierColor;
                material.SetColor("_Color", saberColor);
                if (material.HasProperty("_EnableEmission") && material.GetFloat("_EnableEmission") > 0.5f)
                    material.SetColor("_EmissionColor", saberColor);
                applied = true;
            }
        }
        return applied;
    }

    public void SetParent(Transform parent)
    {
        GameObject.transform.SetParent(parent, false);
        GameObject.transform.position = parent.position;
        GameObject.transform.rotation = parent.rotation;
    }

    public void SetLength(float length) =>
        GameObject.transform.localScale = GameObject.transform.localScale with { z = length };

    public void SetWidth(float width) =>
        GameObject.transform.localScale = GameObject.transform.localScale with { x = width, y = width };

    public void Destroy()
    {
        if (GameObject != null)
        {
            GameObject.Destroy();
            foreach (var material in ownedMaterials)
                UnityEngine.Object.Destroy(material);
        }
    }
}
