using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct SquadSpectatorCycleDiagnosticResult(
        bool RightMouseBound,
        bool Advanced,
        bool SkippedDowned,
        bool Wrapped,
        bool Localized)
    {
        public bool Valid => RightMouseBound && Advanced && SkippedDowned && Wrapped && Localized;
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
        var savedCamera = GetViewport().GetCamera3D();
        var rightMouseBound = InputMap.ActionGetEvents(GameInputActions.Aim)
            .Any(@event => @event is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right
            });
        var advanced = false;
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

            var advanceAccepted = TryHandleSquadSpectatorCycleInput(rightMouseBound);
            _localPlayerDowned = false;
            advanced = advanceAccepted
                && expectedNext is not null
                && ReferenceEquals(_spectatedMate, expectedNext)
                && IsLivingSpectatorTarget(_spectatedMate);

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
                    "RMB SWITCH TEAMMATE")
                == "\u53f3\u952e\u5207\u6362\u5b58\u6d3b\u961f\u53cb";
        }
        finally
        {
            _localPlayerDowned = savedPlayerDowned;
            _localPlayerEliminated = savedPlayerEliminated;
            _localPlayerDownedTimer = savedDownedTimer;
            _player.UiLocked = savedUiLocked;
            _spectatedMate = savedSpectatedMate;
            _demolitionObjectiveSpectatorActive = savedObjectiveSpectator;
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
            skippedDowned,
            wrapped,
            localized);
    }
}
