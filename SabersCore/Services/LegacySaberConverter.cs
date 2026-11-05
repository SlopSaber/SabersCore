extern alias LegacyCustomSaber;

using System;
using SaberComponents.Components;
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
    // The original mod stored the trail length as a frame count at 30 frames per second.
    private const float LegacyTrailFramesPerSecond = 30f;

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
            trail.top = legacy.PointEnd;
            trail.bottom = legacy.PointStart;
            trail.material = legacy.TrailMaterial;
            trail.length = Mathf.Clamp01(legacy.Length / LegacyTrailFramesPerSecond);
            trail.useTrailColor = legacy.colorType == Legacy.ColorType.CustomColor;
            trail.colorSchemeType = legacy.colorType == Legacy.ColorType.RightSaber ? ColorSchemeType.RightSaber : ColorSchemeType.LeftSaber;
            trail.trailColor = legacy.TrailColor;
            trail.multiplierColor = legacy.MultiplierColor;
        }

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
