using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct SquadSpectatorCycleDiagnosticResult(
        bool RightMouseBound,
        bool Advanced,
        bool EliminatedMouseClick,
        bool SkippedDowned,
        bool Wrapped,
        bool Localized)
    {
        public bool Valid => RightMouseBound
            && Advanced
            && EliminatedMouseClick
            && SkippedDowned
            && Wrapped
            && Localized;
    }

    private SquadSpectatorCycleDiagnosticResult ValidateSquadSpectatorCycleForDiagnostics(
        SquadMate downedProbe)
    {
        var livingTargets = LivingSpectatorTargetsBySlot();
        if (livingTargets.Length != 2
            || !IsInstanceValid(downedProbe)
            || !downedProbe.IsDowned)
        {
            return default;
        }

        var savedPlayerDowned = _localPlayerDowned;
        var savedPlayerEliminated = _localPlayerEliminated;
        var savedDownedTimer = _localPlayerDownedTimer;
        var savedUiLocked = _player.UiLocked;
        var savedSpectatedMate = _spectatedMate;
        var savedObjectiveSpectator = _demolitionObjectiveSpectatorActive;
        var savedClickPending = _spectatorCycleClickPending;
        var savedCamera = GetViewport().GetCamera3D();
        var rightMouseBound = InputMap.ActionGetEvents(GameInputActions.Aim)
            .Any(@event => @event is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right
            });
        var advanced = false;
        var eliminatedMouseClick = false;
        var skippedDowned = false;
        var wrapped = false;
        var localized = false;

        try
        {
            _localPlayerDowned = true;
            _localPlayerEliminated = false;
            _player.UiLocked = true;
            BeginSquadMateView();
            _spectatedMate = livingTargets[0];
            ActivateSquadMateView();
            var initialMate = _spectatedMate;
            var initialIndex = Array.FindIndex(
                livingTargets,
                candidate => ReferenceEquals(candidate, initialMate));
            var expectedNext = initialIndex < 0
                ? null
                : livingTargets[(initialIndex + 1) % livingTargets.Length];

            _localPlayerDowned = false;
            _localPlayerEliminated = true;
            _Input(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true
            });
            UpdateSquadSpectatorCamera();
            var advanceAccepted = expectedNext is not null
                && ReferenceEquals(_spectatedMate, expectedNext);
            _localPlayerDowned = false;
            _localPlayerEliminated = true;
            eliminatedMouseClick = advanceAccepted
                && IsLivingSpectatorTarget(_spectatedMate);

            _Input(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Right,
                Pressed = true
            });
            UpdateSquadSpectatorCamera();
            advanced = eliminatedMouseClick
                && ReferenceEquals(_spectatedMate, initialMate)
                && IsLivingSpectatorTarget(_spectatedMate);

            _spectatedMate = livingTargets[^1];
            var wrapAccepted = CycleLivingSpectatorTarget();
            skippedDowned = downedProbe.IsDowned
                && !ReferenceEquals(_spectatedMate, downedProbe)
                && IsLivingSpectatorTarget(_spectatedMate);
            wrapped = wrapAccepted && ReferenceEquals(_spectatedMate, initialMate);

            var callsign = initialMate?.Callsign ?? string.Empty;
            localized = GameLocalization.Format(
                    "spectating_teammate_named",
                    "zh",
                    "SPECTATING  //  {0}",
                    callsign)
                == $"\u6b63\u5728\u89c2\u6218  //  {callsign}"
                && GameLocalization.Get(
                    "spectator_switch_hint",
                    "zh",
                    "LMB/RMB SWITCH TEAMMATE")
                == "\u9f20\u6807\u70b9\u51fb\u5207\u6362\u5b58\u6d3b\u961f\u53cb";
        }
        finally
        {
            _localPlayerDowned = savedPlayerDowned;
            _localPlayerEliminated = savedPlayerEliminated;
            _localPlayerDownedTimer = savedDownedTimer;
            _player.UiLocked = savedUiLocked;
            _spectatedMate = savedSpectatedMate;
            _demolitionObjectiveSpectatorActive = savedObjectiveSpectator;
            _spectatorCycleClickPending = savedClickPending;
            if (savedCamera is not null && IsInstanceValid(savedCamera))
            {
                savedCamera.MakeCurrent();
            }
            else
            {
                RestoreLocalPlayerView();
            }
        }

        return new SquadSpectatorCycleDiagnosticResult(
            rightMouseBound,
            advanced,
            eliminatedMouseClick,
            skippedDowned,
            wrapped,
            localized);
    }
}
