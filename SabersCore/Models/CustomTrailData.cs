using SaberComponents.Models;
using UnityEngine;

namespace SabersCore.Models;

internal class CustomTrailData : ITrailData
{
    public CustomTrailData(
        Material? material,
        float lengthSeconds,
        ColorSchemeType colorSchemeType,
        bool useColorBoostEvents,
        bool useTrailColor,
        Color customColor,
        Color colorMultiplier,
        Vector3 trailTopOffset,
        Vector3 trailBottomOffset)
    {
        Material = material;
        Materials = material is null ? [] : [material];
        LengthSeconds = lengthSeconds;
        UseTrailColor = useTrailColor;
        ColorSchemeType = colorSchemeType;
        UseColorBoostEvents = useColorBoostEvents;
        CustomColor = customColor;
        ColorMultiplier = colorMultiplier;
        TrailTopOffset = trailTopOffset;
        TrailBottomOffset = trailBottomOffset;
        Colorizer = new LegacyTrailColorizer(this);
    }

    public Vector3 TrailTopOffset { get; }
    public Vector3 TrailBottomOffset { get; }
    public Material? Material { get; }
    public Material[] Materials { get; }
    
    public float LengthSeconds { get; }
    
    public ColorSchemeType ColorSchemeType { get; }
    public bool UseColorBoostEvents { get; }
    
    public bool UseTrailColor { get; }
    public Color CustomColor { get; }
    public Color ColorMultiplier { get; }
    public ITrailColorizer Colorizer { get; }
}
