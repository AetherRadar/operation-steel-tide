using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredOperatorAnimator
{
    private static readonly string[] RequiredAnimations =
    {
        "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk",
        "ready_idle", "ready_walk", "ready_run", "ready_sprint",
        "ready_crouch_idle", "ready_crouch_walk",
        "aim_walk", "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk",
        "prone_idle", "prone_crawl", "aim_idle", "hit", "death", "downed",
        "revive_kneel", "revived"
    };

    private static readonly string[] ActionAnimations =
    {
        "shoot", "reload", "melee", "throw", "interact", "pickup", "heal",
        "jump_start", "jump_loop", "jump_land", "slide_start", "slide_loop", "slide_exit"
    };

    private static readonly string[] PistolAnimations =
    {
        "pistol_ready_idle", "pistol_ready_walk", "pistol_ready_run", "pistol_ready_sprint",
        "pistol_ready_crouch_idle", "pistol_ready_crouch_walk",
        "pistol_aim_idle", "pistol_aim_walk", "pistol_aim_run", "pistol_aim_sprint",
        "pistol_aim_crouch_idle", "pistol_aim_crouch_walk", "pistol_shoot",
        "pistol_prone_idle", "pistol_prone_crawl", "pistol_reload"
    };

    private static readonly string[] RifleProneAnimations = { "rifle_prone_idle", "rifle_prone_crawl" };

    private static readonly HashSet<string> NonLoopingAnimations = new(StringComparer.Ordinal)
    {
        "hit", "death", "revived", "shoot", "reload", "melee", "throw", "interact",
        "pickup", "heal", "jump_start", "jump_land", "slide_start", "slide_exit",
        "pistol_shoot", "pistol_reload"
    };

    private readonly AnimationPlayer _player;
    private readonly AuthoredOperatorVisual _visual;
    private readonly AuthoredLocomotionSpeeds _locomotionSpeeds;
    private readonly AuthoredUpperBodyActionPlayer _upperBodyAction;
    private string _current = string.Empty;
    private float _overrideRemaining;
    private float _hitCooldownRemaining;
    private int _overrideWeaponVersion;

    public AuthoredOperatorAnimator(AuthoredOperatorVisual visual)
    {
        _visual = visual;
        _locomotionSpeeds = new AuthoredLocomotionSpeeds(visual.VisualId);
        _player = visual.AnimationPlayer;
        _player.ProcessMode = Node.ProcessModeEnum.Always;
        // Actors choose and sample their authored clip in the same physics
        // tick that moves the body, then refresh its authored weapon socket.
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        foreach (var group in new[] { RequiredAnimations, ActionAnimations, PistolAnimations, RifleProneAnimations })
        {
            foreach (var name in group)
            {
                if (!_player.HasAnimation(name))
                {
                    throw new InvalidOperationException($"Animated operator is missing action {name}.");
                }
                _player.GetAnimation(name).LoopMode = NonLoopingAnimations.Contains(name)
                    ? Animation.LoopModeEnum.None
                    : Animation.LoopModeEnum.Linear;
            }
        }
        _upperBodyAction = new AuthoredUpperBodyActionPlayer(_player);
        _player.AnimationFinished += OnAnimationFinished;
        Play("idle", 1.0f, immediate: true);
    }

    public string CurrentAnimation => _current;
    public int AnimationCount => RequiredAnimations.Length + ActionAnimations.Length + PistolAnimations.Length + RifleProneAnimations.Length;
    public int BaseAnimationCount => RequiredAnimations.Length;
    public int ActionAnimationCount => ActionAnimations.Length;
    public double DeathAnimationDuration => _player.GetAnimation("death").Length;
    public bool DeathPoseCompleted { get; private set; }
    public string UpperBodyAction => _upperBodyAction.CurrentAction;
    public double UpperBodyActionPosition => _upperBodyAction.CurrentPosition;
    public bool HasAnimation(string name) => _player.HasAnimation(name);

    public void Update(
        float delta,
        float speed,
        bool weaponReadied,
        bool prone,
        bool crouched,
        bool aiming,
        bool downed,
        bool reviving,
        bool dead,
        bool airborne = false)
    {
        _hitCooldownRemaining = Mathf.Max(0.0f, _hitCooldownRemaining - delta);
        if (dead || downed || reviving || prone || !weaponReadied)
        {
            _upperBodyAction.Cancel();
        }
        if (dead || downed || reviving || prone || _overrideWeaponVersion != _visual.WeaponAttachmentVersion)
        {
            _overrideRemaining = 0.0f;
        }
        if (speed > 0.08f && _current is "shoot" or "pistol_shoot")
        {
            // A whole-body standing shot clip must not repeatedly replace
            // the legs while the actor is still travelling. Keep its authored
            // recoil on the upper body while the gait resumes underneath.
            if (_overrideRemaining > 0.0f && !dead && !downed && !reviving && !prone && weaponReadied)
            {
                _upperBodyAction.Play("shoot", _visual.UsesPistolCarryPose,
                    _visual.WeaponAttachmentVersion, _overrideRemaining);
            }
            _overrideRemaining = 0.0f;
        }
        if (_overrideRemaining > 0.0f && !dead && !downed)
        {
            _overrideRemaining = Mathf.Max(0.0f, _overrideRemaining - delta);
            if (_overrideRemaining > 0.0f)
            {
                AdvanceAndRefresh(delta);
                return;
            }
        }

        var moving = speed > 0.08f;
        string next;
        var playbackSpeed = 1.0f;
        if (dead)
        {
            next = "death";
        }
        else if (downed)
        {
            next = "downed";
        }
        else if (reviving)
        {
            next = "revive_kneel";
        }
        else if (airborne)
        {
            next = "jump_loop";
        }
        else if (prone)
        {
            next = moving ? "prone_crawl" : "prone_idle";
            if (weaponReadied && !_visual.UsesPistolCarryPose)
            {
                next = "rifle_" + next;
            }
            playbackSpeed = moving ? speed / 1.1f : 1.0f;
        }
        else if (crouched)
        {
            next = SelectWeaponPose(aiming, weaponReadied, moving ? "crouch_walk" : "crouch_idle");
            playbackSpeed = moving ? speed / _locomotionSpeeds.ForGait("crouch_walk") : 1.0f;
        }
        else if (!moving)
        {
            next = SelectWeaponPose(aiming, weaponReadied, "idle");
        }
        else
        {
            var gait = speed >= 4.2f ? "sprint" : speed >= 2.35f ? "run" : "walk";
            next = SelectWeaponPose(aiming, weaponReadied, gait);
            // Preserve the DCC stride distance: a blocked or slowly moving
            // actor must not keep taking full-speed steps. Each family uses
            // the same lower-body cycle for unarmed, rifle, and pistol poses.
            var authoredSpeed = _locomotionSpeeds.ForGait(gait);
            playbackSpeed = speed / authoredSpeed;
        }
        Play(next, playbackSpeed);
        AdvanceAndRefresh(delta);
    }

    private static string SelectWeaponPose(bool aiming, bool weaponReadied, string pose)
        => aiming ? "aim_" + pose : weaponReadied ? "ready_" + pose : pose;

    public void SetRestingPose(bool weaponReadied)
    {
        _overrideRemaining = 0.0f;
        _hitCooldownRemaining = 0.0f;
        _upperBodyAction.Cancel();
        Play(weaponReadied ? "ready_idle" : "idle", 1.0f, immediate: true);
        _visual.RefreshWeaponPose();
    }

    public bool PlayHit()
    {
        if (_hitCooldownRemaining > 0.0f || IsPronePose(_current)
            || _current is "downed" or "death" or "revive_kneel")
        {
            return false;
        }
        _hitCooldownRemaining = 0.62f;
        PlayOverride("hit", 0.22f);
        return true;
    }

    public bool PlayAction(string name, float duration, float playbackSpeed = 1.0f)
    {
        if (!_player.HasAnimation(name))
        {
            throw new InvalidOperationException($"Animated operator is missing action {name}.");
        }
        if (name is "shoot" or "reload" && IsPronePose(_current))
        {
            return true;
        }
        if (name is "reload" or "shoot")
        {
            _upperBodyAction.Play(
                name,
                _visual.UsesPistolCarryPose,
                _visual.WeaponAttachmentVersion,
                Mathf.Max(0.08f, duration));
            return true;
        }
        _hitCooldownRemaining = 0.0f;
        PlayOverride(name, Mathf.Max(0.08f, duration), playbackSpeed);
        return true;
    }

    public void PlayRevived() => PlayOverride("revived", 1.15f);

    private void OnAnimationFinished(StringName name)
        => DeathPoseCompleted = name == "death";

    private void PlayOverride(string name, float duration, float playbackSpeed = 1.0f)
    {
        _upperBodyAction.Cancel();
        _overrideWeaponVersion = _visual.WeaponAttachmentVersion;
        _overrideRemaining = duration;
        Play(name, playbackSpeed, immediate: true);
    }

    private static bool IsLocomotion(string name)
        => name.EndsWith("walk", StringComparison.Ordinal)
            || name.EndsWith("run", StringComparison.Ordinal)
            || name.EndsWith("sprint", StringComparison.Ordinal)
            || name.EndsWith("crawl", StringComparison.Ordinal);

    private static bool IsPronePose(string name)
        => name.Contains("prone_", StringComparison.Ordinal);

    private void Play(string name, float playbackSpeed, bool immediate = false)
    {
        if (_visual.UsesPistolCarryPose
            && (name.StartsWith("ready_", StringComparison.Ordinal)
                || name.StartsWith("aim_", StringComparison.Ordinal)
                || name.StartsWith("prone_", StringComparison.Ordinal)
                || name is "shoot" or "reload"))
        {
            name = "pistol_" + name;
        }
        if (!immediate && _current == name)
        {
            _player.SpeedScale = playbackSpeed;
            return;
        }
        DeathPoseCompleted = false;
        var preserveStride = !immediate && IsLocomotion(_current) && IsLocomotion(name);
        var phase = preserveStride && _player.CurrentAnimationLength > 0.0
            ? _player.CurrentAnimationPosition / _player.CurrentAnimationLength
            : 0.0;
        _current = name;
        _player.SpeedScale = playbackSpeed;
        _player.Play(name, immediate ? 0.0 : 0.16);
        if (immediate)
        {
            _player.Seek(0.0, update: true);
        }
        _player.Advance(0.0);
        if (preserveStride)
        {
            _player.Seek(phase * _player.GetAnimation(name).Length, update: true);
        }
    }

    private void AdvanceAndRefresh(float delta)
    {
        _player.Advance(Mathf.Max(0.0f, delta));
        _upperBodyAction.Advance(
            Mathf.Max(0.0f, delta), _visual.UsesPistolCarryPose, _visual.WeaponAttachmentVersion);
        _visual.RefreshWeaponPose();
    }
}
