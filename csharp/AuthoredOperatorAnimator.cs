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

    private static readonly HashSet<string> LoopingAnimations = new(StringComparer.Ordinal)
    {
        "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk",
        "ready_idle", "ready_walk", "ready_run", "ready_sprint",
        "ready_crouch_idle", "ready_crouch_walk",
        "aim_walk", "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk",
        "prone_idle", "prone_crawl", "aim_idle", "downed", "revive_kneel"
    };

    private readonly AnimationPlayer _player;
    private readonly AuthoredOperatorVisual _visual;
    private string _current = string.Empty;
    private float _overrideRemaining;
    private float _hitCooldownRemaining;

    public AuthoredOperatorAnimator(AuthoredOperatorVisual visual)
    {
        _visual = visual;
        _player = visual.AnimationPlayer;
        _player.ProcessMode = Node.ProcessModeEnum.Always;
        // The owning actors select clips from their deterministic physics
        // step.  Drive the mixer manually from that same call so the weapon
        // solver can consume the pose sampled for this tick.  A built-in
        // Physics callback runs after the CharacterBody3D parent (tree-order),
        // which would leave RefreshWeaponPose one physics frame behind.
        _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        foreach (var name in RequiredAnimations)
        {
            if (!_player.HasAnimation(name))
            {
                throw new InvalidOperationException($"Animated operator is missing action {name}.");
            }
            var animation = _player.GetAnimation(name);
            if (animation is not null)
            {
                animation.LoopMode = LoopingAnimations.Contains(name)
                    ? Animation.LoopModeEnum.Linear
                    : Animation.LoopModeEnum.None;
            }
        }
        Play("idle", 1.0f, immediate: true);
    }

    public string CurrentAnimation => _current;
    public int AnimationCount => RequiredAnimations.Length + ActionAnimationCount;
    public int BaseAnimationCount => RequiredAnimations.Length;
    public int ActionAnimationCount
    {
        get
        {
            var count = 0;
            foreach (var action in ActionAnimations)
            {
                if (HasAnimation(action)
                    || action == "jump_loop" && HasAnimation("jump")
                    || action == "slide_loop" && HasAnimation("slide"))
                {
                    count++;
                }
            }
            return count;
        }
    }
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
        if (_overrideRemaining > 0.0f && !dead && !downed)
        {
            _overrideRemaining = Mathf.Max(0.0f, _overrideRemaining - delta);
            if (_overrideRemaining > 0.0f)
            {
                AdvanceAndRefresh(delta);
                return;
            }
        }

        var moving = speed > 0.16f;
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
            next = HasAnimation("jump_loop")
                ? "jump_loop"
                : HasAnimation("jump_start") ? "jump_start" : "aim_idle";
        }
        else if (prone)
        {
            next = moving ? "prone_crawl" : "prone_idle";
            playbackSpeed = moving ? Mathf.Clamp(speed / 1.1f, 0.72f, 1.35f) : 1.0f;
        }
        else if (crouched)
        {
            next = moving
                ? SelectWeaponPose(aiming, weaponReadied, "aim_crouch_walk", "ready_crouch_walk", "crouch_walk")
                : SelectWeaponPose(aiming, weaponReadied, "aim_crouch_idle", "ready_crouch_idle", "crouch_idle");
            playbackSpeed = moving ? Mathf.Clamp(speed / 2.4f, 0.72f, 1.4f) : 1.0f;
        }
        else if (!moving)
        {
            next = SelectWeaponPose(aiming, weaponReadied, "aim_idle", "ready_idle", "idle");
        }
        else if (speed >= 4.5f)
        {
            // The unarmed sprint clip tucks the torso too far forward. Armed
            // operators retain the authored sprint/aim-sprint silhouette so
            // weapon handling remains consistent; unarmed operators accelerate
            // the upright run cycle instead.
            next = weaponReadied || aiming
                ? SelectWeaponPose(aiming, weaponReadied, "aim_sprint", "ready_sprint", "sprint")
                : "run";
            playbackSpeed = weaponReadied || aiming
                ? Mathf.Clamp(speed / 5.2f, 0.86f, 1.25f)
                : Mathf.Clamp(speed / 3.6f, 0.9f, 1.3f);
        }
        else if (speed >= 2.75f)
        {
            next = SelectWeaponPose(aiming, weaponReadied, "aim_run", "ready_run", "run");
            playbackSpeed = Mathf.Clamp(speed / 3.6f, 0.78f, 1.35f);
        }
        else
        {
            next = SelectWeaponPose(aiming, weaponReadied, "aim_walk", "ready_walk", "walk");
            playbackSpeed = Mathf.Clamp(speed / 2.1f, 0.72f, 1.35f);
        }
        Play(next, playbackSpeed);
        AdvanceAndRefresh(delta);
        ApplyGroundingCorrection(downed || dead);
    }

    private static string SelectWeaponPose(
        bool aiming,
        bool weaponReadied,
        string aimPose,
        string readyPose,
        string unarmedPose)
        => aiming ? aimPose : weaponReadied ? readyPose : unarmedPose;

    public void SetRestingPose(bool weaponReadied)
    {
        _overrideRemaining = 0.0f;
        _hitCooldownRemaining = 0.0f;
        Play(weaponReadied ? "ready_idle" : "idle", 1.0f, immediate: true);
        _visual.RefreshWeaponPose(weaponReadied ? "ready_idle" : "idle");
        ApplyGroundingCorrection(false);
    }

    public bool PlayHit()
    {
        if (_hitCooldownRemaining > 0.0f)
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
            return false;
        }
        _hitCooldownRemaining = 0.0f;
        PlayOverride(name, Mathf.Max(0.08f, duration), playbackSpeed);
        return true;
    }

    public void PlayRevived()
        => PlayOverride("revived", 1.15f);

    private void PlayOverride(string name, float duration, float playbackSpeed = 1.0f)
    {
        _overrideRemaining = duration;
        Play(name, playbackSpeed, immediate: true);
    }

    private void Play(string name, float playbackSpeed, bool immediate = false)
    {
        name = ResolveAnimation(name);
        if (!immediate && _current == name)
        {
            _player.SpeedScale = playbackSpeed;
            return;
        }
        _current = name;
        _player.SpeedScale = playbackSpeed;
        _player.Play(name, immediate ? 0.0 : 0.16);
        // Play() queues the first key for the next mixer notification.  Apply
        // that key now because the caller solves the weapon socket in the
        // same physics step.  This also makes hit/revive overrides switch
        // without displaying one frame of the previous locomotion pose.
        _player.Advance(0.0);
    }

    private string ResolveAnimation(string name)
    {
        if (_player.HasAnimation(name))
        {
            return name;
        }
        if (name is "jump_loop" && _player.HasAnimation("jump"))
        {
            return "jump";
        }
        if (name is "slide_loop" && _player.HasAnimation("slide"))
        {
            return "slide";
        }
        return name switch
        {
            "jump_start" or "jump_loop" or "jump_land" => "aim_idle",
            "slide_start" or "slide_loop" or "slide_exit" => "crouch_idle",
            "shoot" => "aim_idle",
            "reload" => "ready_idle",
            "melee" or "throw" or "interact" or "pickup" or "heal" => "idle",
            _ => "idle"
        };
    }

    private void AdvanceAndRefresh(float delta)
    {
        _player.Advance(Mathf.Max(0.0f, delta));
        _visual.RefreshWeaponPose(_current);
    }

    private void ApplyGroundingCorrection(bool downed)
    {
        // Downed/death clips are authored around a standing root.  Lower the
        // visual to the actor's capsule base so the body rests on the ground
        // instead of hovering above it.
        _visual.Root.Position = downed
            ? Vector3.Down * 0.46f
            : Vector3.Zero;
    }
}
