using SaberComponents.Models;
using SabersCore.Models;
using SabersCore.Utilities.Common;
using SabersCore.Utilities.Extensions;
using UnityEngine;
using Zenject;

namespace SabersCore.Components;

public class CustomSaberTrail : SaberTrail
{
    private readonly SaberMovementData customTrailMovementData = new();

    private TimeHelper timeHelper = null!;
    private Transform trailTop = null!;
    private Transform trailBottom = null!;
    private ITrailData trailData = null!;

    [Inject]
    public void Construct(
        TimeHelper timeHelper,
        InitData initData)
    {
        this.timeHelper = timeHelper;
        trailTop = initData.TrailTop;
        trailBottom = initData.TrailBottom;
        trailData = initData.TrailData;
        gameObject.layer = 12;
        _movementData = customTrailMovementData;
    }

    public float OverrideWidth { private get; set; } = 1f;
    public bool UseWidthOverride { private get; set; }
    
    public ITrailData TrailData => trailData;

    public void SetColorScheme(ColorScheme colorScheme) => SetColor(colorScheme);

    public void SetBoostColors(ColorScheme colorScheme, bool isBoostOn) =>
        UpdateBoostColors(colorScheme, isBoostOn);

    public void SetSpecificColor(Color color)
    {
        if (!trailData.UseTrailColor && trailData.ColorSchemeType is ColorSchemeType.LeftSaber or ColorSchemeType.RightSaber)
            SetColor(color);
    }

    public void SetColor(ColorScheme colorScheme)
    {
        if (trailData.UseTrailColor)
        {
            SetColor(trailData.CustomColor);
            return;
        }
        var color = colorScheme.GetColorByType(trailData.ColorSchemeType);
        SetColor(color);
    }
    
    public void UpdateBoostColors(ColorScheme colorScheme, bool isBoostOn)
    {
        if (trailData.UseTrailColor || !trailData.UseColorBoostEvents) return;
        var color = colorScheme.GetBoostColorByType(trailData.ColorSchemeType, isBoostOn);
        SetColor(color);
    }

    public void SetColor(Color color, SaberType saberType)
    {
        if (trailData.UseTrailColor) return;
        if ((saberType == SaberType.SaberA && trailData.ColorSchemeType == ColorSchemeType.LeftSaber)
            || (saberType == SaberType.SaberB && trailData.ColorSchemeType == ColorSchemeType.RightSaber))
        {
            SetColor(color);
        }
    }
    
    private void SetColor(Color color)
    {
        _color = color * trailData.ColorMultiplier;
        foreach (var trailMaterial in _trailRenderer._meshRenderer.materials)
        {
            var poiyomiTrail = trailMaterial.GetTag("ElectroTrail", false, "0") == "1";
            var materialColor = poiyomiTrail ? CustomSaber.SaturatePoiyomiColor(_color) : _color;
            trailMaterial.SetColor(MaterialProperties.Color, materialColor);
            if (poiyomiTrail)
                trailMaterial.SetColor("_EmissionColor", materialColor);
        }
    }
    
    private new void Start()
    {
        // Ignored
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        var topPos = trailTop.position;
        var bottomPos = trailBottom.position;
        
        customTrailMovementData.AddNewData(
            topPos,
            UseWidthOverride ? GetOverrideWidthBottom(OverrideWidth) : bottomPos,
            timeHelper.Time);

        return;
        
        Vector3 GetOverrideWidthBottom(float trailWidth)
        {
            float distance = Vector3.Distance(topPos, bottomPos);
            return distance.Approximately(0) ? bottomPos 
                : Vector3.LerpUnclamped(topPos, bottomPos, trailWidth / distance);
        }
    }
    
    public record InitData(Transform TrailTop, Transform TrailBottom, ITrailData TrailData);
}
