extern alias LegacyCustomSaber;

using System;
using System.Collections.Generic;
using SaberComponents.Components;
using SabersCore.Components;
using SabersCore.Utilities.Common;
using SaberComponents.Models;
using UnityEngine;
using UnityEngine.Events;
using Legacy = LegacyCustomSaber::CustomSaber;
using LegacyDescriptor = LegacyCustomSaber::SaberDescriptor;

namespace SabersCore.Services;

/// <summary>
/// Converts components from .saber files, which were authored against the original CustomSaber
/// assembly, into their SaberComponents equivalents.
/// </summary>
internal static class LegacySaberConverter
{
    // The original mod stored the trail length in frames, where 30 frames matched the default trail.
    private const float LegacyTrailFrames = 30f;
    private const float DefaultTrailSeconds = 0.4f;

    /// <summary>
    /// Loads the legacy assembly so Unity can bind the scripts referenced by a .saber bundle.
    /// </summary>
    public static void EnsureLoaded() => _ = typeof(LegacyDescriptor).Assembly;

    public static void Convert(GameObject saber)
    {
        foreach (var legacy in saber.GetComponentsInChildren<LegacyDescriptor>(true))
        {
            var descriptor = legacy.GetComponent<SaberDescriptor>() ?? legacy.gameObject.AddComponent<SaberDescriptor>();
            descriptor.SaberName = legacy.SaberName;
            descriptor.AuthorName = legacy.AuthorName;
            descriptor.Description = legacy.Description;
            descriptor.CoverImage = legacy.CoverImage != null ? legacy.CoverImage.texture : null;
        }

        foreach (var legacy in saber.GetComponentsInChildren<Legacy.CustomTrail>(true))
        {
            var trail = legacy.gameObject.AddComponent<CustomTrail>();
            legacy.gameObject.AddComponent<LegacyCustomTrail>();
            trail.top = legacy.PointEnd;
            trail.bottom = legacy.PointStart;
            trail.material = legacy.TrailMaterial;
            trail.length = Mathf.Clamp01(legacy.Length / LegacyTrailFrames * DefaultTrailSeconds);
            trail.useTrailColor = legacy.colorType == Legacy.ColorType.CustomColor;
            trail.colorSchemeType = legacy.colorType == Legacy.ColorType.RightSaber ? ColorSchemeType.RightSaber : ColorSchemeType.LeftSaber;
            trail.trailColor = legacy.TrailColor;
            trail.multiplierColor = legacy.MultiplierColor;
        }

        AddMaterialColorers(saber);

        foreach (var legacy in saber.GetComponentsInChildren<Legacy.EventManager>(true))
        {
            var events = legacy.gameObject.AddComponent<EventManager>();
            events.noteCut = legacy.OnSlice ?? new UnityEvent();
            events.comboBroken = legacy.OnComboBreak ?? new UnityEvent();
            events.multiplierUp = legacy.MultiplierUp ?? new UnityEvent();
            events.saberStartColliding = legacy.SaberStartColliding ?? new UnityEvent();
            events.saberStopColliding = legacy.SaberStopColliding ?? new UnityEvent();
            events.levelStarted = legacy.OnLevelStart ?? new UnityEvent();
            events.levelFailed = legacy.OnLevelFail ?? new UnityEvent();
            events.levelEnded = legacy.OnLevelEnded ?? new UnityEvent();
            CopyEvent(legacy.OnComboChanged, events.comboChanged);
            CopyEvent(legacy.OnAccuracyChanged, events.accuracyChanged);
        }

        foreach (var legacy in saber.GetComponentsInChildren<Legacy.ComboReachedEvent>(true))
        {
            var filter = legacy.gameObject.AddComponent<ComboReachedEvent>();
            filter.comboTarget = legacy.ComboTarget;
            filter.nthComboReached = legacy.NthComboReached ?? new UnityEvent();
        }

        foreach (var legacy in saber.GetComponentsInChildren<Legacy.EveryNthComboFilter>(true))
        {
            var filter = legacy.gameObject.AddComponent<EveryNthComboFilter>();
            filter.comboStep = legacy.ComboStep;
            filter.nthComboReached = legacy.NthComboReached ?? new UnityEvent();
        }

        foreach (var legacy in saber.GetComponentsInChildren<Legacy.AccuracyReachedEvent>(true))
        {
            var filter = legacy.gameObject.AddComponent<AccuracyReachedEvent>();
            filter.target = legacy.Target;
            filter.accuracyReached = legacy.OnAccuracyReachTarget ?? new UnityEvent();
            filter.accuracyHigherThanTarget = legacy.OnAccuracyHigherThanTarget ?? new UnityEvent();
            filter.accuracyLowerThanTarget = legacy.OnAccuracyLowerThanTarget ?? new UnityEvent();
        }
    }

    // Legacy sabers marked colorable materials with _CustomColors, or with _Glow/_Bloom before that.
    private static void AddMaterialColorers(GameObject saber)
    {
        List<int> indices = [];
        foreach (var renderer in saber.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.GetComponent<MaterialColorer>() != null)
            {
                continue;
            }

            var side = GetSide(renderer.transform);
            if (side is null)
            {
                continue;
            }

            indices.Clear();
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                if (IsColorable(materials[i]))
                {
                    indices.Add(i);
                }
            }

            if (indices.Count == 0)
            {
                continue;
            }

            var colorer = renderer.gameObject.AddComponent<MaterialColorer>();
            colorer.meshRenderer = renderer;
            colorer.propertyName = "_Color";
            colorer.colorSchemeType = side.Value;
            colorer.multiplierColor = Color.white;
            renderer.gameObject.AddComponent<LegacyColorTargets>().materialIndices = indices.ToArray();
        }
    }

    private static ColorSchemeType? GetSide(Transform transform)
    {
        for (var current = transform; current != null; current = current.parent)
        {
            if (current.name == "LeftSaber") return ColorSchemeType.LeftSaber;
            if (current.name == "RightSaber") return ColorSchemeType.RightSaber;
        }

        return null;
    }

    private static bool IsColorable(Material? material) =>
        material != null && material.HasProperty(MaterialProperties.Color) &&
        (material.HasProperty(MaterialProperties.CustomColors)
            ? material.GetFloat(MaterialProperties.CustomColors) > 0
            : (material.HasProperty(MaterialProperties.Glow) && material.GetFloat(MaterialProperties.Glow) > 0) ||
              (material.HasProperty(MaterialProperties.Bloom) && material.GetFloat(MaterialProperties.Bloom) > 0));

    // Generic event subclasses differ between the assemblies but share UnityEventBase's
    // serialized persistent calls, so they are copied through serialization.
    private static void CopyEvent(UnityEventBase? source, UnityEventBase target)
    {
        if (source == null)
        {
            return;
        }

        try
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn($"Could not copy legacy saber event listeners: {ex.Message}");
        }
    }
}
