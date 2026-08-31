using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Narrow runtime surface used by survivor extraction. The world remains the
/// composition root while this service owns the takeover state machine.
/// </summary>
internal interface ISurvivorExtractionRuntime
{
    bool TakeoverEligible { get; }
    bool MissionEnded { get; }
    bool DeparturePlaying { get; }
    bool HasActionableRevive { get; }
    bool CountdownActive { get; }
    float CountdownRemaining { get; }
    bool AircraftBoardingReady { get; }
    int PassengerSeatCount { get; }
    Vector3 ExtractionPoint { get; }
    IReadOnlyList<SquadMate> SquadMates { get; }

    bool IsInsideExtractionZone(Vector3 position);
    void ResetPlayerExtractionCall();
    void PauseCountdownForRescue();
    void BeginCountdown();
    void AdvanceCountdown(float delta);
    void CompleteExtraction();
    void BeginRally();
    void SetRallyDestination(SquadMate mate, Vector3 destination);
    void FinishRally(bool orderChanged);
    void ResetBoardingCount();
    void BoardMate(SquadMate mate, int seatIndex);
}

/// <summary>
/// Coordinates offline AI-only extraction after the local operator has been
/// permanently eliminated. It owns all mutable takeover collections and state.
/// </summary>
internal sealed class SurvivorExtractionService
{
    private static readonly Vector3[] ReadyOffsets =
    {
        new(-2.2f, 0.1f, 2.0f),
        new(2.2f, 0.1f, 2.0f),
        new(0.0f, 0.1f, 3.2f)
    };

    private readonly ISurvivorExtractionRuntime _runtime;
    private readonly HashSet<ulong> _ralliedMateIds = new();
    private readonly List<SquadMate> _livingMates = new(3);

    internal SurvivorExtractionService(ISurvivorExtractionRuntime runtime)
    {
        _runtime = runtime;
    }

    internal bool TakeoverActive { get; private set; }

    /// <summary>
    /// Runs the takeover state machine. A true result means the normal
    /// player-authored extraction sequence must not run this frame.
    /// </summary>
    internal bool Update(float delta)
    {
        if (!_runtime.TakeoverEligible)
        {
            Reset();
            return false;
        }

        var starting = !TakeoverActive;
        TakeoverActive = true;
        if (starting)
        {
            _runtime.ResetPlayerExtractionCall();
        }
        if (_runtime.MissionEnded || _runtime.DeparturePlaying)
        {
            return true;
        }

        if (_runtime.HasActionableRevive)
        {
            _ralliedMateIds.Clear();
            _runtime.PauseCountdownForRescue();
            return true;
        }

        var livingMates = CollectLivingMates();
        if (livingMates.Count == 0)
        {
            return true;
        }

        Rally(livingMates);
        var mateInside = AnyMateInside(livingMates);
        if (!_runtime.CountdownActive)
        {
            if (mateInside)
            {
                _runtime.BeginCountdown();
            }
            return true;
        }

        // Once called, extraction remains active while survivors keep rallying.
        // A brief combat displacement does not restart the complete hold timer.
        _runtime.AdvanceCountdown(delta);
        if (_runtime.CountdownRemaining <= 0.0f
            && _runtime.AircraftBoardingReady
            && mateInside)
        {
            // Completion may synchronously ask this service to count and board,
            // which reuses _livingMates. Do not iterate the prior list afterward.
            _runtime.CompleteExtraction();
        }
        return true;
    }

    internal (int Ready, int Total) CountReady()
    {
        var ready = 0;
        var livingMates = CollectLivingMates();
        for (var index = 0; index < livingMates.Count; index++)
        {
            var mate = livingMates[index];
            if (_runtime.IsInsideExtractionZone(mate.GlobalPosition))
            {
                ready++;
            }
        }
        return (ready, livingMates.Count);
    }

    internal void BoardReadyMates()
    {
        _runtime.ResetBoardingCount();
        var seatCount = _runtime.PassengerSeatCount;
        if (seatCount <= 0)
        {
            return;
        }

        var boarded = 0;
        var livingMates = CollectLivingMates();
        for (var index = 0; index < livingMates.Count; index++)
        {
            var mate = livingMates[index];
            if (!_runtime.IsInsideExtractionZone(mate.GlobalPosition))
            {
                continue;
            }
            if (boarded >= seatCount)
            {
                break;
            }
            _runtime.BoardMate(mate, boarded);
            boarded++;
        }
    }

    internal void Reset()
    {
        if (!TakeoverActive)
        {
            return;
        }
        TakeoverActive = false;
        _ralliedMateIds.Clear();
        _livingMates.Clear();
    }

    private List<SquadMate> CollectLivingMates()
    {
        _livingMates.Clear();
        var squadMates = _runtime.SquadMates;
        for (var index = 0; index < squadMates.Count; index++)
        {
            var mate = squadMates[index];
            if (IsLivingAi(mate))
            {
                _livingMates.Add(mate);
            }
        }
        return _livingMates;
    }

    private void Rally(List<SquadMate> livingMates)
    {
        _runtime.BeginRally();
        var orderChanged = false;
        for (var index = 0; index < livingMates.Count; index++)
        {
            var mate = livingMates[index];
            if (!_ralliedMateIds.Add(mate.GetInstanceId()))
            {
                continue;
            }
            _runtime.SetRallyDestination(
                mate,
                _runtime.ExtractionPoint
                    + ReadyOffsets[Mathf.Min(index, ReadyOffsets.Length - 1)]);
            orderChanged = true;
        }
        _runtime.FinishRally(orderChanged);
    }

    private bool AnyMateInside(List<SquadMate> livingMates)
    {
        for (var index = 0; index < livingMates.Count; index++)
        {
            var mate = livingMates[index];
            if (_runtime.IsInsideExtractionZone(mate.GlobalPosition))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsLivingAi(SquadMate mate)
        => GodotObject.IsInstanceValid(mate)
            && mate.IsInsideTree()
            && !mate.IsHumanProxy
            && !mate.IsNetworkProxy
            && !mate.IsDowned
            && !mate.IsBodyBag
            && !mate.IsExtractionPassenger
            && mate.ProcessMode != Node.ProcessModeEnum.Disabled;
}
