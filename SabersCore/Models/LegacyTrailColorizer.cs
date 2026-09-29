using System.Collections.Generic;
using SabersCore.Utilities.Extensions;
using UnityEngine;

namespace SabersCore.Models;

internal sealed class LegacyTrailColorizer : ITrailColorizer
{
    private readonly ITrailData trailData;

    public LegacyTrailColorizer(ITrailData trailData) => this.trailData = trailData;

    public IEnumerable<TrailColorInfo> GetPropertiesWithColors(ColorScheme colorScheme)
    {
        var color = trailData.UseTrailColor
            ? trailData.CustomColor
            : colorScheme.GetColorByType(trailData.ColorSchemeType);
        return GetColorInfo(color);
    }

    public IEnumerable<TrailColorInfo> GetPropertiesWithBoostColors(ColorScheme colorScheme, bool boost)
    {
        var color = trailData.UseTrailColor
            ? trailData.CustomColor
            : trailData.UseColorBoostEvents
                ? colorScheme.GetBoostColorByType(trailData.ColorSchemeType, boost)
                : colorScheme.GetColorByType(trailData.ColorSchemeType);
        return GetColorInfo(color);
    }

    public IEnumerable<TrailColorInfo> GetDefault() =>
        GetColorInfo(trailData.UseTrailColor ? trailData.CustomColor : Color.white);

    private IEnumerable<TrailColorInfo> GetColorInfo(Color color)
    {
        if (trailData.Material is { } material)
            yield return new(material, 0, "_Color", color * trailData.ColorMultiplier, true);
    }
}
