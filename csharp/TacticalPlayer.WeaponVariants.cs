using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private Node3D _platformSignatureRoot = null!;

    internal int WeaponSignaturePartCountForDiagnostics
        => IsInstanceValid(_platformSignatureRoot) ? _platformSignatureRoot.GetChildCount() : 0;
    internal bool UsesDesertEagleReportForDiagnostics
        => EquippedWeapon.Platform == WeaponPlatform.DesertEagle
        && IsInstanceValid(_gunAudio)
        && _gunAudio.Stream is AudioStreamWav { Data.Length: > 16000 };
    internal bool UsesGsh18ReportForDiagnostics
        => EquippedWeapon.Platform == WeaponPlatform.GSh18
        && IsInstanceValid(_gunAudio)
        && _gunAudio.Stream is AudioStreamWav { Data.Length: > 14000 };

    private void RefreshPlatformSignatureVisual()
    {
        if (!IsInstanceValid(_platformSignatureRoot))
        {
            return;
        }
        var children = _platformSignatureRoot.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            child.QueueFree();
        }

        var black = TacticalSurfaceLibrary.WeaponFinish(new Color(0.035f, 0.045f, 0.042f), 0.76f, 0.28f);
        var steel = TacticalSurfaceLibrary.WeaponFinish(new Color(0.22f, 0.25f, 0.24f), 0.9f, 0.2f);
        var green = TacticalSurfaceLibrary.WeaponFinish(new Color(0.12f, 0.2f, 0.12f), 0.14f, 0.72f);
        switch (EquippedWeapon.Platform)
        {
            case WeaponPlatform.AWM:
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.19f, 0.055f, 0.62f)),
                    new Vector3(0, 0.13f, -0.08f), Vector3.Zero, steel);
                MeshPart(_platformSignatureRoot, Cylinder(0.022f, 0.24f),
                    new Vector3(0.11f, 0.08f, 0.03f), new Vector3(0, 0, Mathf.Pi / 2), steel);
                MeshPart(_platformSignatureRoot, Cylinder(0.018f, 0.38f),
                    new Vector3(-0.11f, -0.2f, -0.73f), new Vector3(0.42f, 0, -0.18f), black);
                MeshPart(_platformSignatureRoot, Cylinder(0.018f, 0.38f),
                    new Vector3(0.11f, -0.2f, -0.73f), new Vector3(0.42f, 0, 0.18f), black);
                break;
            case WeaponPlatform.VSS:
                MeshPart(_platformSignatureRoot, Cylinder(0.075f, 0.62f),
                    new Vector3(0, 0.015f, -0.76f), new Vector3(Mathf.Pi / 2, 0, 0), black);
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.035f, 0.22f, 0.48f)),
                    new Vector3(-0.05f, 0, 0.37f), new Vector3(0, 0.12f, -0.32f), green);
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.035f, 0.22f, 0.48f)),
                    new Vector3(0.05f, 0, 0.37f), new Vector3(0, -0.12f, 0.32f), green);
                break;
            case WeaponPlatform.DesertEagle:
                var chrome = TacticalSurfaceLibrary.WeaponFinish(new Color(0.48f, 0.5f, 0.46f), 0.96f, 0.12f);
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.145f, 0.075f, 0.36f)),
                    new Vector3(0, 0.075f, -0.03f), Vector3.Zero, chrome);
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.15f, 0.032f, 0.15f)),
                    new Vector3(0, -0.08f, -0.22f), new Vector3(0.2f, 0, 0), black);
                MeshPart(_platformSignatureRoot, Cylinder(0.018f, 0.095f),
                    new Vector3(0.085f, 0.095f, 0.03f), new Vector3(0, 0, Mathf.Pi / 2), chrome);
                break;
            case WeaponPlatform.GSh18:
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.132f, 0.026f, 0.19f)),
                    new Vector3(0, 0.145f, -0.035f), Vector3.Zero, steel);
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.014f, 0.032f, 0.052f)),
                    new Vector3(0.074f, 0.12f, -0.03f), Vector3.Zero, black);
                break;
        }
    }
}
