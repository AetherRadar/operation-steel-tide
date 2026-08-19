namespace OperationSteelTide;

/// <summary>
/// Pure in-match economy shared by competitive demolition modes: both squads start with the
/// same wallet, round outcomes pay fixed rewards, and loss streaks escalate like Counter-
/// Strike. This wallet is independent of the extraction progression profile.
/// </summary>
public sealed class DemolitionEconomy
{
    public const int StartingFunds = 800;
    public const int MaximumFunds = 9000;
    public const int WinReward = 3000;
    public const int LossBaseReward = 1900;
    public const int LossStreakBonus = 500;
    public const int MaximumLossStreak = 4;
    public const int PlantBonus = 300;
    public const int DefuseBonus = 300;
    public const int BuyCap = 4400;
    public const int EcoThreshold = 2000;

    public int Funds { get; private set; } = StartingFunds;
    public int LossStreak { get; private set; }

    public void Reset()
    {
        Funds = StartingFunds;
        LossStreak = 0;
    }

    public void HalftimeSwap(DemolitionEconomy other)
    {
        (Funds, other.Funds) = (other.Funds, Funds);
        (LossStreak, other.LossStreak) = (other.LossStreak, LossStreak);
    }

    public int RecordRound(bool won, bool objectiveCompleted)
    {
        if (won)
        {
            LossStreak = 0;
            Funds = Cap(Funds + WinReward);
            return WinReward;
        }
        LossStreak = System.Math.Min(LossStreak + 1, MaximumLossStreak);
        var reward = LossBaseReward + System.Math.Min(LossStreak - 1, MaximumLossStreak - 1) * LossStreakBonus;
        if (objectiveCompleted)
        {
            reward += PlantBonus;
        }
        Funds = Cap(Funds + reward);
        return reward;
    }

    public int Spend(int amount)
    {
        var spent = System.Math.Clamp(amount, 0, Funds);
        Funds -= spent;
        return spent;
    }

    public void ApplyNetworkFunds(int funds)
        => Funds = Cap(funds);

    public bool CanFullBuy => Funds >= BuyCap;
    public bool ShouldEco => Funds < EcoThreshold;

    private static int Cap(int funds) => System.Math.Clamp(funds, 0, MaximumFunds);
}
