using SixActual.Sim;

namespace SixActual.Sim.State;

public struct BattleUnit
{
    public int Id;

    public int Def;

    public int Player;

    public int Alive;

    public Fp X;

    public Fp Y;

    public Fp Z;

    public int Facing;

    public int Hp;

    public int Ammo;

    public int Fuel;

    public int Morale;

    public int Suppression;

    public int Flags;

    public int Reload;

    public int HasGoal;

    public Fp GoalX;

    public Fp GoalY;

    public int HasVia;

    public Fp ViaX;

    public Fp ViaY;

    public int Spotted;

    public int Controlled;

    public int HomeX;

    public int HomeY;
}

public struct MapBlock
{
    public int X;

    public int Y;

    public int W;

    public int H;

    public int Height;

    public int BlocksMove;

    public int BlocksSight;
}

public struct PendingImpact
{
    public int Active;

    public int TickDue;

    public Fp X;

    public Fp Y;

    public int Radius;

    public int Damage;

    public int Shooter;
}

public sealed class BattleState
{
    public const int FlagTracked = 1;

    public const int FlagOptics = 2;

    public const int FlagEngine = 4;

    public const int FlagTurret = 8;

    public const int FlagBail = 16;

    public const int FlagFire = 32;

    public int Tick;

    public ulong Seed;

    public ulong Rng;

    public int MapSize;

    public int TickCap;

    public int Aggression0;

    public int Aggression1;

    public int XoCommand;

    public int Winner;

    public int Weather;

    public int LastShotActive;

    public Fp LastShotX0;

    public Fp LastShotY0;

    public Fp LastShotX1;

    public Fp LastShotY1;

    public UnitDef[] Defs = [];

    public BattleUnit[] Units = [];

    public MapBlock[] Blocks = [];

    public PendingImpact[] Impacts = new PendingImpact[16];
}
