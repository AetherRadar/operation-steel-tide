using System;
using Godot;

namespace OperationSteelTide;

public static class SquadNetworkRuntime
{
    private const string RuntimeNodeName = "SquadNetworkRuntime";

    public static SquadNetwork GetOrCreate(SceneTree tree)
    {
        var existing = tree.Root.GetNodeOrNull<SquadNetwork>(RuntimeNodeName);
        if (existing is not null)
        {
            existing.ProcessMode = Node.ProcessModeEnum.Always;
            return existing;
        }

        throw new InvalidOperationException(
            $"Missing {RuntimeNodeName} autoload. Check project.godot before creating the world.");
    }
}
