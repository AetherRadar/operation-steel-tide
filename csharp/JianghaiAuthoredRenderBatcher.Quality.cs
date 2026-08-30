using Godot;

namespace OperationSteelTide;

internal sealed partial class JianghaiAuthoredRenderBatcher
{
    public int ApplyQuality(int qualityTier)
    {
        var normalizedTier = Mathf.Clamp(qualityTier, 0, 2);
        var distanceScale = VisibilityDistanceScale(normalizedTier);
        var shadowCasterSourceCount = 0;
        foreach (var batch in _batches)
        {
            if (!GodotObject.IsInstanceValid(batch.Instance))
            {
                continue;
            }

            var endDistance = batch.Policy.BaseVisibilityRange * distanceScale
                + batch.BatchRadius;
            batch.RequiredVisibilityRangeEnd = endDistance;
            batch.Instance.VisibilityRangeEnd = endDistance;
            batch.Instance.VisibilityRangeEndMargin = Mathf.Min(28.0f, endDistance * 0.12f);
            batch.Instance.VisibilityRangeFadeMode =
                GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
            var allowShadow = normalizedTier switch
            {
                0 => !batch.Policy.IsDetail,
                1 => !batch.Policy.IsFineDetail && batch.Policy.WorldDiagonal > 4.0f,
                _ => !batch.Policy.IsFineDetail
            };
            batch.Instance.CastShadow = batch.Policy.AlwaysDisableShadow || !allowShadow
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : batch.Policy.AuthoredShadowSetting;
            if (batch.Instance.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off)
            {
                shadowCasterSourceCount += batch.ExpectedRootTransforms.Length;
            }
        }
        return shadowCasterSourceCount;
    }

    public static float VisibilityDistanceScale(int qualityTier)
        => Mathf.Clamp(qualityTier, 0, 2) switch
        {
            0 => 0.68f,
            1 => 0.84f,
            _ => 1.0f
        };

    public static JianghaiRenderQualityPolicy CreateQualityPolicy(
        MeshInstance3D meshInstance)
    {
        var localSize = meshInstance.GetAabb().Size;
        var globalScale = meshInstance.GlobalTransform.Basis.Scale.Abs();
        var worldSize = new Vector3(
            localSize.X * globalScale.X,
            localSize.Y * globalScale.Y,
            localSize.Z * globalScale.Z);
        var diagonal = worldSize.Length();
        var name = meshInstance.Name.ToString();
        var isInteriorLiner = IsInteriorLiner(meshInstance);
        var isValleyEnvironment = ContainsAny(
            name,
            "OldCityFoundation",
            "JianghaiPerimeterGround",
            "JianghaiMountainMassif");
        var baseVisibilityRange = isInteriorLiner
            ? JianghaiInteriorShellValidator.RequiredVisibilityRange
            : isValleyEnvironment ? 1200.0f : diagonal switch
        {
            <= 1.2f => 105.0f,
            <= 4.0f => 180.0f,
            <= 12.0f => 285.0f,
            _ => 460.0f
        };
        var isFineDetail = diagonal <= 1.2f
            || ContainsAny(name, "Screen", "Indicator", "Fastener", "Text", "Cable", "Lens");
        var isDetail = isInteriorLiner
            || diagonal <= 12.0f
            || ContainsAny(
                name,
                "Aircon",
                "Rollershutter",
                "Trashbag",
                "UtilityBox",
                "Barrel",
                "Crate",
                "SecurityCamera",
                "Television");
        var alwaysDisableShadow = isInteriorLiner
            || isValleyEnvironment
            || diagonal <= 0.45f
            || ContainsAny(name, "ScreenTrace", "StatusScreen", "Indicator", "Fastener", "Text");
        return new JianghaiRenderQualityPolicy(
            diagonal,
            baseVisibilityRange,
            isDetail,
            isFineDetail,
            alwaysDisableShadow,
            meshInstance.CastShadow);
    }
}
