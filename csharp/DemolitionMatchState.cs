using System;

namespace OperationSteelTide;

public enum DemolitionTeam
{
    Attackers,
    Defenders
}

public readonly record struct DemolitionRoundResult(
    DemolitionTeam Winner,
    int CompletedRounds,
    int AttackerScore,
    int DefenderScore,
    bool EnteredOvertime,
    bool MatchComplete);

/// <summary>
/// Pure demolition score and overtime rules. The first twelve rounds form regulation;
/// a 6-6 tie enters win-by-two overtime.
/// </summary>
public sealed class DemolitionMatchState
{
    public const int RegulationRounds = 12;
    public const int RegulationWinsRequired = 7;
    public const int OvertimeLeadRequired = 2;

    public int AttackerScore { get; private set; }
    public int DefenderScore { get; private set; }
    public int CompletedRounds { get; private set; }
    public bool IsOvertime { get; private set; }
    public bool IsComplete { get; private set; }
    public DemolitionTeam? Winner { get; private set; }
    public int CurrentRound => CompletedRounds + 1;

    public void Reset()
    {
        AttackerScore = 0;
        DefenderScore = 0;
        CompletedRounds = 0;
        IsOvertime = false;
        IsComplete = false;
        Winner = null;
    }

    public DemolitionRoundResult RecordRound(DemolitionTeam winner)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Cannot record a round after the demolition match is complete.");
        }

        if (winner == DemolitionTeam.Attackers)
        {
            AttackerScore++;
        }
        else
        {
            DefenderScore++;
        }
        CompletedRounds++;

        var enteredOvertime = false;
        if (!IsOvertime)
        {
            if (AttackerScore >= RegulationWinsRequired || DefenderScore >= RegulationWinsRequired)
            {
                CompleteLeadingTeam();
            }
            else if (CompletedRounds >= RegulationRounds)
            {
                if (AttackerScore == DefenderScore)
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
        else if (Math.Abs(AttackerScore - DefenderScore) >= OvertimeLeadRequired)
        {
            CompleteLeadingTeam();
        }

        return new DemolitionRoundResult(
            winner,
            CompletedRounds,
            AttackerScore,
            DefenderScore,
            enteredOvertime,
            IsComplete);
    }

    private void CompleteLeadingTeam()
    {
        IsComplete = true;
        Winner = AttackerScore > DefenderScore
            ? DemolitionTeam.Attackers
            : DemolitionTeam.Defenders;
    }
}
