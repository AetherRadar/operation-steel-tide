using Godot;

namespace OperationSteelTide;

internal sealed partial class JianghaiAuthoredRenderBatcher
{
    /// <summary>Frees safe leaf sources after their MultiMeshes own the visible instances.</summary>
    public (int Released, int Retained, int Blocked) ReleaseLeafSourceNodes()
    {
        var released = 0;
        var retained = 0;
        var blocked = 0;
        for (var index = _sources.Count - 1; index >= 0; index--)
        {
            var instance = _sources[index].Instance;
            if (!GodotObject.IsInstanceValid(instance))
            {
                _sources.RemoveAt(index);
                continue;
            }
            if (instance.IsInGroup(JianghaiInteriorPopulationService.EnterableSourceGroup))
            {
                retained++;
                continue;
            }
            if (instance.GetChildCount() != 0)
            {
                blocked++;
                continue;
            }
            instance.Free();
            _sources.RemoveAt(index);
            released++;
        }
        return (released, retained, blocked);
    }
}
