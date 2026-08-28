using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private const int LocalWeaponReportVoiceCount = 5;
    private Node3D _platformSignatureRoot = null!;
    private readonly List<AudioStreamPlayer> _gunAudioVoices = new();
    private int _nextGunAudioVoice;

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
    internal bool PlayerWeaponAudioReadyForDiagnostics
        => IsInstanceValid(_gunAudio)
        && _gunAudio.Stream is AudioStreamWav { Data.Length: > 14000 }
        && _gunAudio.VolumeDb >= -6.5f;
    internal bool PlayerWeaponAudioIsLocalForDiagnostics
        => IsInstanceValid(_gunAudio) && ReferenceEquals(_gunAudio.GetParent(), _camera);
    internal bool PlayerWeaponAudioPlayingForDiagnostics
        => ActiveWeaponAudioVoiceCountForDiagnostics > 0;
    internal int WeaponAudioVoiceCountForDiagnostics => _gunAudioVoices.Count;
    internal int ActiveWeaponAudioVoiceCountForDiagnostics
    {
        get
        {
            var active = 0;
            foreach (var voice in _gunAudioVoices)
            {
                if (IsInstanceValid(voice) && voice.Playing)
                {
                    active++;
                }
            }
            return active;
        }
    }
    internal float PlayerWeaponAudioVolumeDbForDiagnostics
        => IsInstanceValid(_gunAudio) ? _gunAudio.VolumeDb : float.NegativeInfinity;
    internal int PlayerWeaponAudioSignatureForDiagnostics
        => PlayerWeaponAudioReadyForDiagnostics
            ? SoundLab.WeaponShotSignature(EquippedWeapon)
            : 0;

    private void PlayLocalWeaponReport()
    {
        if (_gunAudioVoices.Count == 0)
        {
            return;
        }

        var selectedIndex = _nextGunAudioVoice % _gunAudioVoices.Count;
        for (var offset = 0; offset < _gunAudioVoices.Count; offset++)
        {
            var candidateIndex = (_nextGunAudioVoice + offset) % _gunAudioVoices.Count;
            if (!_gunAudioVoices[candidateIndex].Playing)
            {
                selectedIndex = candidateIndex;
                break;
            }
        }

        var voice = _gunAudioVoices[selectedIndex];
        if (voice.Playing)
        {
            voice.Stop();
        }
        voice.PitchScale = _rng.RandfRange(0.96f, 1.04f);
        voice.Play();
        _nextGunAudioVoice = (selectedIndex + 1) % _gunAudioVoices.Count;
    }

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
            case WeaponPlatform.GSh18:
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.132f, 0.026f, 0.19f)),
                    new Vector3(0, 0.145f, -0.035f), Vector3.Zero, steel);
                MeshPart(_platformSignatureRoot, Box(new Vector3(0.014f, 0.032f, 0.052f)),
                    new Vector3(0.074f, 0.12f, -0.03f), Vector3.Zero, black);
                break;
        }
    }
}
