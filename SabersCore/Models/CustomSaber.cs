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

    public GameObject GameObject { get; }
    public EventManager EventManager { get; }

    public CustomSaber(GameObject gameObject)
    {
        GameObject = gameObject;
        GameObject.SetLayerRecursively(12);
        if (gameObject.transform.root.name.IndexOf("iSF-ElectroCoachingSilver", StringComparison.Ordinal) >= 0)
        {
            if (gameObject.name == "RightSaber")
            {
                var donor = gameObject.GetComponentsInChildren<MeshRenderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .FirstOrDefault(material => material != null &&
                        material.name.StartsWith("DeepBlue", StringComparison.Ordinal) &&
                        material.shader != null && material.shader.name == "ElectroSaber/Body");
                Plugin.Log.Notice($"Electro diagnostic: colored blade donor found={donor != null}");
                if (donor != null)
                {
                    var material = new Material(donor);
                    material.SetColor("_Color", new Color(0f, 0.5f, 0f, 1f));
                    material.SetColor("_SaberColor", Color.white);
                    material.SetFloat("_UseSaberColor", 0f);
                    material.SetFloat("_UseMatcap", 0f);
                    material.SetFloat("_Glow", 0.5f);
                    material.SetTexture("_MainTex", Texture2D.whiteTexture);
                    foreach (var renderer in gameObject.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                }
            }
            else if (gameObject.name == "LeftSaber")
            {
                var count = 0;
                foreach (var renderer in gameObject.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null ||
                        material.shader.name != "ElectroSaber/Body" || !material.HasProperty("_UseMatcap") ||
                        material.GetFloat("_UseMatcap") == 0f)
                        continue;
                    material.SetFloat("_UseMatcap", 0f);
                    count++;
                }
                Plugin.Log.Notice($"Electro diagnostic: LeftSaber disabled matcap on {count} material slots");
            }
        }
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
            colorer.materialPropertyBlock ??= new();
            colorer.meshRenderer.GetPropertyBlock(colorer.materialPropertyBlock);
            colorer.materialPropertyBlock.SetColor(colorer.propertyName, color * colorer.multiplierColor);
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
                colorer.materialPropertyBlock.SetColor(colorer.propertyName, color * colorer.multiplierColor);
                colorer.meshRenderer.SetPropertyBlock(colorer.materialPropertyBlock);
            }
        }
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
        }
    }
}
