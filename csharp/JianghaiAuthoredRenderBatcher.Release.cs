using Godot;

namespace OperationSteelTide;

internal readonly record struct JianghaiRenderSourceRelease(
    int PreReleaseSourceCount,
    int ExpectedRetained,
    int Released,
    int Retained,
    int Blocked)
{
    public bool Valid => Released + Retained + Blocked == PreReleaseSourceCount
        && Blocked == 0
        && Retained == ExpectedRetained;
}

internal sealed partial class JianghaiAuthoredRenderBatcher
{
    /// <summary>Frees safe leaf sources after their MultiMeshes own the visible instances.</summary>
    public JianghaiRenderSourceRelease ReleaseLeafSourceNodes()
    {
        var preReleaseSourceCount = _sources.Count;
        var expectedRetained = 0;
        foreach (var source in _sources)
        {
            if (GodotObject.IsInstanceValid(source.Instance)
                && source.Instance.IsInGroup(
                    JianghaiInteriorPopulationService.EnterableSourceGroup))
            {
                expectedRetained++;
            }
        }
        var released = 0;
        var retained = 0;
        var blocked = 0;
        for (var index = _sources.Count - 1; index >= 0; index--)
        {
            var instance = _sources[index].Instance;
            if (!GodotObject.IsInstanceValid(instance))
            {
                _sources.RemoveAt(index);
                blocked++;
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
        return new JianghaiRenderSourceRelease(
            preReleaseSourceCount,
            expectedRetained,
            released,
            retained,
            blocked);
    }
}
