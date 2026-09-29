using UnityEngine;

namespace SabersCore.Models;

public record TrailColorInfo(Material Material, int MaterialIndex, string PropertyName, Color Color, bool ApplyToVertexColor);
