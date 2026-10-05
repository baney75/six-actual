namespace SixActual.Sim;

public static class Collapse
{
    public static int Step(int[] alive)
    {
        var damage = 0;
        for (var i = 1; i < alive.Length; i++)
        {
            if (alive[i] == 1 && alive[i - 1] == 0)
            {
                alive[i] = 0;
                damage += 10;
            }
        }

        return damage;
    }
}
