namespace SixActual.Sim;

public static class Strategy
{
    public const int ProvinceCount = 8;

    public sealed class State
    {
        public int Turn;

        public int AtWar = 1;

        public int Tension;

        public int Nukes = 1;

        public int Winner = -1;

        public int[] Owner = [0, 0, 0, 0, 1, 1, 1, 1];

        public int[] Industry = [2, 2, 2, 2, 2, 2, 2, 2];
    }

    public static State Start() => new();

    public static void SetPeace(State state) => state.AtWar = 0;

    public static void EndTurn(State state)
    {
        if (state.Winner >= 0)
        {
            return;
        }

        state.Turn++;
        if (state.AtWar == 1)
        {
            for (var i = 0; i < ProvinceCount - 1; i++)
            {
                if (state.Owner[i] == state.Owner[i + 1])
                {
                    continue;
                }

                var taker = state.Industry[i] >= state.Industry[i + 1] ? i : i + 1;
                var lost = taker == i ? i + 1 : i;
                state.Owner[lost] = state.Owner[taker];
                state.Industry[taker]++;
                state.Tension++;
                break;
            }
        }

        if (state.Tension >= 4 && state.Nukes > 0 && state.AtWar == 1)
        {
            for (var i = 0; i < ProvinceCount; i++)
            {
                if (state.Owner[i] != 0)
                {
                    state.Industry[i] = Math.Max(0, state.Industry[i] - 2);
                    break;
                }
            }

            state.Nukes--;
            state.Tension += 3;
        }

        var owner = state.Owner[0];
        var all = true;
        for (var i = 1; i < ProvinceCount; i++)
        {
            if (state.Owner[i] != owner)
            {
                all = false;
            }
        }

        if (all)
        {
            state.Winner = owner;
        }
        else if (state.Turn >= 24)
        {
            state.Winner = 2;
        }
    }

    public static State Run(int turns)
    {
        var state = Start();
        for (var i = 0; i < turns && state.Winner < 0; i++)
        {
            EndTurn(state);
        }

        return state;
    }
}
