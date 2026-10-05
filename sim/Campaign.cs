namespace SixActual.Sim;

public static class Campaign
{
    public const int OperationsPerEra = 6;

    public static readonly string[] Eras = ["ww2", "cold_war", "modern"];

    public sealed class State
    {
        public int Era;

        public int Operation;

        public int Wins;

        public int Losses;

        public int Xp;

        public int Wounded;
    }

    public static State Start(int era)
    {
        if (era < 0 || era >= Eras.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(era));
        }

        return new State { Era = era };
    }

    public static string OperationName(State state) => $"{Eras[state.Era]}-{state.Operation + 1}";

    public static void ApplyResult(State state, bool playerWon)
    {
        if (IsFinished(state))
        {
            return;
        }

        if (playerWon)
        {
            state.Wins++;
            state.Xp += 10;
        }
        else
        {
            state.Losses++;
            state.Wounded = 1;
            state.Xp += 2;
        }

        state.Operation++;
    }

    public static bool IsFinished(State state) => state.Operation >= OperationsPerEra || state.Losses >= 3;
}
