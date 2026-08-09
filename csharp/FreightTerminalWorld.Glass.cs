using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateBreakableGlass()
    {
        await WaitFrames(6);
        var fields = GetTree().GetNodesInGroup(BreakableGlassField.GroupName)
            .OfType<BreakableGlassField>()
            .Where(IsInstanceValid)
            .ToArray();
        var paneCount = fields.Sum(field => field.PaneCount);
        var frameInstances = fields.Sum(field => field.FrameInstanceCount);
        var field = fields.FirstOrDefault(candidate => candidate.PaneCount >= 2);
        var rayFrom = Vector3.Zero;
        var rayTo = Vector3.Zero;
        var paneIndex = -1;
        var rayReady = field is not null
            && field.TryGetIntactPaneRay(out rayFrom, out rayTo, out paneIndex);
        var shattered = false;
        var collisionDisabled = false;
        var repeatIgnored = false;
        if (rayReady && field is not null)
        {
            shattered = BreakableGlassField.TryShatterAlongRay(
                GetWorld3D(),
                rayFrom,
                rayTo,
                30.0f,
                rayFrom.DirectionTo(rayTo),
                out _,
                false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            collisionDisabled = field.IsPaneShattered(paneIndex)
                && field.IsPaneCollisionDisabled(paneIndex);
            repeatIgnored = !BreakableGlassField.TryShatterAlongRay(
                GetWorld3D(),
                rayFrom,
                rayTo,
                30.0f,
                rayFrom.DirectionTo(rayTo),
                out _,
                false);
        }

        var fieldsBatched = fields.Length > 0
            && fields.Length <= ResidentialTowerSpecs.Length + ResidentialSkyLinks.Sum(link => link.Floors.Length) + 2;
        var panesDense = paneCount >= 1200;
        var framesComplete = frameInstances == paneCount * 5;
        var residentialTracked = ResidentialGlassPaneCount == paneCount;
        var valid = rayReady
            && shattered
            && collisionDisabled
            && repeatIgnored
            && fieldsBatched
            && panesDense
            && framesComplete
            && residentialTracked;
        GD.Print($"GLASS_CHECK valid={valid} fields={fields.Length} panes={paneCount} frames={frameInstances} ray_ready={rayReady} shattered={shattered} collision_disabled={collisionDisabled} repeat_ignored={repeatIgnored} batched={fieldsBatched} dense={panesDense} tracked={residentialTracked}");
        GD.Print($"GLASS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
