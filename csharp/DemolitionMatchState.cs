using System;

namespace OperationSteelTide;

public enum DemolitionTeam
{
    Attackers,
    Defenders
}

public enum DemolitionRoundEndReason
{
    Elimination,
    BombDetonated,
    BombDefused,
    TimeExpired
}

public static class DemolitionRoundRules
{
    public static bool EliminationEndsRound(DemolitionTeam eliminatedTeam, bool devicePlanted)
        => !devicePlanted || eliminatedTeam == DemolitionTeam.Defenders;
}

public readonly record struct DemolitionRoundResult(
    bool PlayerTeamWon,
    DemolitionTeam Winner,
    int CompletedRounds,
    int PlayerScore,
    int OpponentScore,
    bool SideSwap,
    bool EnteredOvertime,
    bool MatchComplete);

/// <summary>
/// MR12 demolition score rules shared by Valorant and Counter-Strike: 12 rounds per
/// half with a side swap at halftime, first to 13 wins, and a win-by-two overtime that
/// swaps sides every four rounds. Scores are tracked per squad, not per side.
/// </summary>
public sealed class DemolitionMatchState
{
    public const int RoundsPerHalf = 12;
    public const int RegulationRounds = RoundsPerHalf * 2;
    public const int RegulationWinsRequired = 13;
    public const int OvertimeLeadRequired = 2;
    public const int OvertimeSwapInterval = 4;

    public int PlayerScore { get; private set; }
    public int OpponentScore { get; private set; }
    public int CompletedRounds { get; private set; }
    public bool IsOvertime { get; private set; }
    public bool IsComplete { get; private set; }
    public DemolitionTeam? Winner { get; private set; }
    public int CurrentRound => CompletedRounds + 1;
    public DemolitionTeam PlayerSide => SideForRound(CurrentRound);

    public void Reset()
    {
        PlayerScore = 0;
        OpponentScore = 0;
        CompletedRounds = 0;
        IsOvertime = false;
        IsComplete = false;
        Winner = null;
    }

    /// <summary>The side the player squad plays on for the given 1-based round number.</summary>
    public DemolitionTeam SideForRound(int roundNumber)
    {
        if (roundNumber <= RoundsPerHalf)
        {
            return DemolitionTeam.Attackers;
        }
        if (roundNumber <= RegulationRounds)
        {
            return DemolitionTeam.Defenders;
        }
        var overtimeRound = roundNumber - RegulationRounds;
        var block = (overtimeRound - 1) / OvertimeSwapInterval;
        return block % 2 == 0 ? DemolitionTeam.Attackers : DemolitionTeam.Defenders;
    }

    public DemolitionRoundResult RecordRound(bool playerTeamWon)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Cannot record a round after the demolition match is complete.");
        }

        if (playerTeamWon)
        {
            PlayerScore++;
        }
        else
        {
            OpponentScore++;
        }
        CompletedRounds++;

        var sideBefore = SideForRound(CompletedRounds);
        var enteredOvertime = false;
        if (!IsOvertime)
        {
            if (PlayerScore >= RegulationWinsRequired || OpponentScore >= RegulationWinsRequired)
            {
                CompleteLeadingTeam();
            }
            else if (CompletedRounds >= RegulationRounds)
            {
                if (PlayerScore == OpponentScore)
                {
                    IsOvertime = true;
                    enteredOvertime = true;
                }
                else
                {
                    CompleteLeadingTeam();
                }
            }
        }
        else if (Math.Abs(PlayerScore - OpponentScore) >= OvertimeLeadRequired)
        {
            CompleteLeadingTeam();
        }

        var sideSwap = !IsComplete && SideForRound(CurrentRound) != sideBefore;
        var winnerSide = playerTeamWon ? sideBefore : OtherSide(sideBefore);
        return new DemolitionRoundResult(
            playerTeamWon,
            winnerSide,
            CompletedRounds,
            PlayerScore,
            OpponentScore,
            sideSwap,
            enteredOvertime,
            IsComplete);
    }

    private static DemolitionTeam OtherSide(DemolitionTeam side)
        => side == DemolitionTeam.Attackers ? DemolitionTeam.Defenders : DemolitionTeam.Attackers;

    private void CompleteLeadingTeam()
    {
        IsComplete = true;
        Winner = PlayerScore > OpponentScore ? DemolitionTeam.Attackers : DemolitionTeam.Defenders;
    }
}
