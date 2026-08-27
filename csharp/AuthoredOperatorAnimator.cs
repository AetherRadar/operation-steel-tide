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

    private static readonly HashSet<string> LoopingAnimations = new(StringComparer.Ordinal)
    {
        "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk",
        "ready_idle", "ready_walk", "ready_run", "ready_sprint",
        "ready_crouch_idle", "ready_crouch_walk",
        "aim_walk", "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk",
        "prone_idle", "prone_crawl", "aim_idle", "downed", "revive_kneel"
    };

    private readonly AnimationPlayer _player;
    private string _current = string.Empty;
    private float _overrideRemaining;
    private float _hitCooldownRemaining;

    public AuthoredOperatorAnimator(AuthoredOperatorVisual visual)
    {
        _player = visual.AnimationPlayer;
        _player.ProcessMode = Node.ProcessModeEnum.Always;
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
    public int AnimationCount => RequiredAnimations.Length;

    public void Update(
        float delta,
        float speed,
        bool weaponReadied,
        bool prone,
        bool crouched,
        bool aiming,
        bool downed,
        bool reviving,
        bool dead)
    {
        _hitCooldownRemaining = Mathf.Max(0.0f, _hitCooldownRemaining - delta);
        if (_overrideRemaining > 0.0f && !dead && !downed)
        {
            _overrideRemaining = Mathf.Max(0.0f, _overrideRemaining - delta);
            if (_overrideRemaining > 0.0f)
            {
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
            next = SelectWeaponPose(aiming, weaponReadied, "aim_sprint", "ready_sprint", "sprint");
            playbackSpeed = Mathf.Clamp(speed / 5.2f, 0.78f, 1.35f);
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

    public void PlayRevived()
        => PlayOverride("revived", 1.15f);

    private void PlayOverride(string name, float duration)
    {
        _overrideRemaining = duration;
        Play(name, 1.0f, immediate: true);
    }

    private void Play(string name, float playbackSpeed, bool immediate = false)
    {
        if (!immediate && _current == name)
        {
            _player.SpeedScale = playbackSpeed;
            return;
        }
        _current = name;
        _player.SpeedScale = playbackSpeed;
        _player.Play(name, immediate ? 0.0 : 0.16);
    }
}
