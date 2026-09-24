using System.Collections;
using System.Linq;
using UnityEngine;

namespace SabersCore.Models;

public sealed class ElectroMaterialProbe : MonoBehaviour
{
    public Material ExpectedMaterial { get; set; } = null!;

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3f);
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).ToArray();
        var expected = materials.Count(material => material == ExpectedMaterial);
        var shaders = string.Join(",", materials.Where(material => material != null)
            .Select(material => material.shader?.name ?? "missing").Distinct());
        Plugin.Log.Notice($"Electro diagnostic after 3s: expected material slots={expected}/{materials.Length}, enabled mesh renderers={renderers.Count(renderer => renderer.enabled)}/{renderers.Length}, shaders={shaders}");
    }
}
