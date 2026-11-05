using UnityEngine;

namespace SabersCore.Components;

/// <summary>
/// Material slots of a converted .saber renderer that take the saber color. Legacy sabers marked
/// individual materials as colorable, so other slots on the same renderer keep their authored color.
/// </summary>
internal class LegacyColorTargets : MonoBehaviour
{
    public int[] materialIndices = [];
}
