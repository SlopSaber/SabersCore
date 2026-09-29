using UnityEngine;

namespace SabersCore.Models;

public record TrailColorInfo(Material Material, int MaterialIndex, string PropertyName, Color Color, bool ApplyToVertexColor)
{
    public TrailColorInfo(AssetComponents.Components.Sabers.TrailColorer colorer, Color color)
        : this(colorer.Material, colorer.MaterialIndex, colorer.PropertyName, color, colorer.ApplyToVertexColor) { }

    public TrailColorInfo(AssetComponents.Components.Sabers.TrailColorer colorer)
        : this(colorer, Color.white) { }
}
