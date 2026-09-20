using System;
using Godot;

namespace OperationSteelTide;

/// <summary>Plays authored combat upper-body tracks over the actor's current locomotion clip.</summary>
internal sealed class AuthoredUpperBodyActionPlayer
{
    private static readonly string[] UpperBodyBones =
    {
        ":Spine", ":Neck", ":Head", ":LeftShoulder", ":RightShoulder",
        ":LeftArm", ":RightArm", ":LeftForeArm", ":RightForeArm",
        ":LeftHand", ":RightHand", ":Hair"
    };

    private readonly AnimationPlayer _player;
    private float _remaining;
    private bool _pistol;
    private int _weaponVersion;

    public AuthoredUpperBodyActionPlayer(AnimationPlayer source)
    {
        _player = new AnimationPlayer
        {
            Name = "AuthoredUpperBodyActionPlayer",
            RootNode = source.RootNode,
            ProcessMode = Node.ProcessModeEnum.Always,
            CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual
        };
        source.GetParent().AddChild(_player);
        var library = new AnimationLibrary();
        foreach (var name in new[] { "reload", "pistol_reload", "shoot", "pistol_shoot" })
        {
            var animation = (Animation)source.GetAnimation(name).Duplicate(true);
            for (var track = animation.GetTrackCount() - 1; track >= 0; track--)
            {
                var path = animation.TrackGetPath(track).ToString();
                var retained = false;
                foreach (var bone in UpperBodyBones)
                {
                    retained |= path.Contains(bone, StringComparison.Ordinal);
                }
                if (!retained)
                {
                    animation.RemoveTrack(track);
                }
            }
            if (animation.GetTrackCount() == 0)
            {
                throw new InvalidOperationException($"Authored {name} contains no upper-body animation tracks.");
            }
            animation.LoopMode = Animation.LoopModeEnum.None;
            library.AddAnimation(name, animation);
        }
        _player.AddAnimationLibrary(string.Empty, library);
    }

    public string CurrentAction => _remaining > 0.0f ? _player.AssignedAnimation.ToString() : string.Empty;
    public double CurrentPosition => _remaining > 0.0f ? _player.CurrentAnimationPosition : 0.0;

    public void Play(string action, bool pistol, int weaponVersion, float duration)
    {
        var name = pistol ? "pistol_" + action : action;
        _pistol = pistol;
        _weaponVersion = weaponVersion;
        _remaining = duration;
        _player.SpeedScale = (float)_player.GetAnimation(name).Length / duration;
        _player.Play(name, customBlend: 0.0);
        // Godot keeps the current time when Play repeats an active clip.
        // Every shot is a new recoil, including automatic fire.
        _player.Seek(0.0, update: false);
    }

    public void Cancel()
    {
        _remaining = 0.0f;
        _player.Stop();
    }

    public void Advance(float delta, bool pistol, int weaponVersion)
    {
        if (_remaining <= 0.0f)
        {
            return;
        }
        if (_pistol != pistol || _weaponVersion != weaponVersion)
        {
            Cancel();
            return;
        }
        _remaining = Mathf.Max(0.0f, _remaining - delta);
        _player.Advance(delta);
    }
}
