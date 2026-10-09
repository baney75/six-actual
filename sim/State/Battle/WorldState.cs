using SixActual.Sim;
using SixActual.Sim.Battle;

namespace SixActual.Sim.State.Battle;

/// <summary>Heightfield plus per-cell ground layers. Heights are decimetres; layers are 0..100.</summary>
public sealed class TerrainGrid
{
    public int SizeM;

    public int CellM;

    public int Cells;

    public int[] HeightDm = [];

    public byte[] Forest = [];

    public byte[] Road = [];

    /// <summary>100 = hedgerow in this cell.</summary>
    public byte[] Hedge = [];

    /// <summary>100 = impassable to ground units (water, building footprint, wall).</summary>
    public byte[] Blocked = [];

    /// <summary>Rubble depth per cell in decimetres (already added to HeightDm); slows tracks, stops wheels.</summary>
    public int[] RubbleDm = [];

    /// <summary>Building index + 1 per cell, 0 = none.</summary>
    public int[] BuildingAt = [];

    /// <summary>SUP-03: 100 = anti-vehicle obstacle (ditch, dragon's teeth, abatis): stops tracked and wheeled
    /// vehicles, infantry cross.</summary>
    public byte[] Obstacle = [];

    /// <summary>SUP-03: 100 = field fortification (trench line, fighting positions): heavy cover for infantry.</summary>
    public byte[] Fort = [];

    /// <summary>SUP-03: minefield density per cell (percent chance a vehicle entering the cell strikes a mine).</summary>
    public byte[] MineDensity = [];

    /// <summary>Team + 1 that laid the mines in the cell (its own units know the lanes), 0 = none.</summary>
    public byte[] MineTeam = [];

    /// <summary>Bumped whenever heights or layers change, so nav grids rebuild (craters, rubble, breaches).</summary>
    public int Version;

    public int Index(int cx, int cy) => (cy * Cells) + cx;

    public bool InCell(int cx, int cy) => cx >= 0 && cy >= 0 && cx < Cells && cy < Cells;

    public int CellOf(Fp metres)
    {
        var m = (int)(metres.Raw >> Fp.FractionalBits);
        return m < 0 ? -1 : m / CellM;
    }
}

public struct UnitState
{
    public int Id;

    public int Def;

    public int Player;

    public int Alive;

    public Fp X;

    public Fp Y;

    public Fp Z;

    /// <summary>Hull heading in binary angle units: 0..65535 is one turn, 0 = east, counter-clockwise.</summary>
    public int Heading;

    public int Hp;

    public int Ammo;

    public int Fuel;

    public int Morale;

    public int Suppression;

    public int Flags;

    /// <summary>Bit p set when player p currently detects this unit.</summary>
    public int SpottedBy;

    public int MovedThisTick;

    public int RevealUntilTick;

    public int HasGoal;

    public Fp GoalX;

    public Fp GoalY;

    /// <summary>Index of the next waypoint in WorldState.Paths[unit index]; -1 = no path.</summary>
    public int PathStep;

    public int LastProgressTick;

    public int NoPath;

    public int MountStart;

    /// <summary>Squad members still fighting (infantry); 1 for vehicles.</summary>
    public int Members;

    /// <summary>0 normal, 1 shaken, 2 pinned, 3 routed, 4 surrendered (SIM-08).</summary>
    public int MoraleState;

    public int Veterancy;

    public int FuelMilli;

    public int HomeX;

    public int HomeY;

    public int RoutedSinceTick;

    /// <summary>Unit is a command post that steadies nearby troops.</summary>
    public int IsHq;

    public int Kills;

    /// <summary>Rules of engagement: 0 fire at will, 1 return fire only, 2 hold fire (CMD-04).</summary>
    public int Roe;

    public int LastHitByTick;

    /// <summary>Index of the last unit that hit this one, for kill credit on burn-outs and bail-outs; -1 none.</summary>
    public int LastAttacker;

    /// <summary>Supply points left (logistics units).</summary>
    public int SupplyLeft;

    /// <summary>Millimetres moved since the last whole kilometre of fuel was charged.</summary>
    public int MetresSinceFuel;

    /// <summary>Aircraft: 1 once its CAS sortie is flown; it then returns to base like Bingo or Winchester.</summary>
    public int SortieDone;

    /// <summary>FLY-01: player index + 1 while a human flies this aircraft (Take Control); 0 = AI pilot.</summary>
    public int Flown;

    /// <summary>SUP-03: ticks of field repair toward the next critical fixed.</summary>
    public int RepairTicks;

    /// <summary>Flown aircraft controls: throttle 0..100, turn and climb stick -100..100.</summary>
    public int Throttle;

    public int StickTurn;

    public int StickClimb;

    /// <summary>Flown aircraft state: airspeed (mm/s), flight-path sine (permille), 1 while stalled.</summary>
    public int AirspeedMmS;

    public int SinGammaPm;

    public int Stalled;

    /// <summary>MEC-04: 1 while falling back home under a retreat order (no fire, ignores pinning).</summary>
    public int Retreating;

    /// <summary>Aircraft: tick it returns from the off-map airbase; 0 = on map.</summary>
    public int OffMapUntil;

    /// <summary>Tick before which the unit does not rescan for targets (nothing was in reach).</summary>
    public int NextScanTick;

    /// <summary>Commander seat owning this unit; -1 = the player controls it directly (CMD-02).</summary>
    public int Seat;

    /// <summary>1 while falling back to resupply/repair (CMD-06 withdrawal).</summary>
    public int Withdrawing;

    public int WasDamaged;

    /// <summary>1 after asking logistics for ammo this time it ran low.</summary>
    public int AskedResupply;

    /// <summary>Deck cost of this unit (support points are paid on its destruction).</summary>
    public int Cost;
}

public struct Building
{
    /// <summary>South-west corner in metres.</summary>
    public int X;

    public int Y;

    public int Nx;

    public int Ny;

    public int Floors;

    public int Material;

    /// <summary>Ground height under the building when built, decimetres.</summary>
    public int BaseDm;

    public int ChunkStart;
}

public struct PlayerState
{
    public int Dp;

    /// <summary>Deployment point remainder in units of 1 / (ticks per minute) of a point.</summary>
    public int DpMilli;

    public int Sp;

    public int Profile;

    public int SpawnX;

    public int SpawnY;

    public int SupplyDelivered;

    public int Deck;

    /// <summary>Index into WorldState.CardStock where this player's card counts start.</summary>
    public int StockStart;

    public int Team;

    public int DpSpent;

    public int DpEarned;
}

/// <summary>A battery's current fire mission (SUP-01).</summary>
public struct FireMission
{
    public int Active;

    public int Battery;

    public int TargetX;

    public int TargetY;

    public int RoundsLeft;

    /// <summary>0 HE, 1 smoke.</summary>
    public int Kind;

    public int NextShotTick;

    public int Mount;
}

/// <summary>A round in flight.</summary>
public struct PendingSupport
{
    public int Def;

    public int Player;

    public int X;

    public int Y;

    /// <summary>Tick the effect arrives (call tick + response delay).</summary>
    public int Arrive;

    /// <summary>Impacts delivered so far (strike runs); -1 once complete.</summary>
    public int Done;
}

/// <summary>SUP-04 electronic warfare zone: Kind 1 radar jamming, 2 comms jamming, 3 decoys (chaff corridor);
/// owned by Team, active until Until.</summary>
public struct EwZone
{
    public int Team;

    public int Kind;

    public int X;

    public int Y;

    public int RadiusM;

    public int Until;
}

public struct Reveal
{
    public int Player;

    public int X;

    public int Y;

    public int RadiusM;

    public int Until;

    public int AirOnly;
}

public struct Shell
{
    public int Active;

    public int LandTick;

    public int X;

    public int Y;

    public int Weapon;

    public int Kind;

    public int Shooter;
}

/// <summary>A counter-battery fix: where an enemy team's radar placed a firing battery.</summary>
public struct BatteryFix
{
    public int Active;

    /// <summary>Team that owns the fix (the radar side).</summary>
    public int Team;

    public int Battery;

    public int X;

    public int Y;

    public int Tick;
}

/// <summary>One commander seat of one player (CMD-01). Seat index = player * 9 + role.</summary>
public struct SeatState
{
    public int Player;

    public int Role;

    /// <summary>0 cautious .. 100 aggressive (CMD-04).</summary>
    public int Aggression;

    /// <summary>0 .. 100: higher tolerates more damage before withdrawing.</summary>
    public int CasualtyTolerance;

    /// <summary>ROE applied to the seat's units (Combat.Roe*).</summary>
    public int Roe;

    /// <summary>0 auto-approve support requests sent to this seat, 1 ask the player (CMD-05).</summary>
    public int ApprovalMode;

    /// <summary>UnitDef kind preferred as target (0 = none).</summary>
    public int PriorityKind;
}

/// <summary>A task in a seat's queue (CMD-03).</summary>
public struct CommandTask
{
    public int Seat;

    /// <summary>CommandTask kinds: see Command.Task* constants.</summary>
    public int Kind;

    public int X;

    public int Y;

    public int X2;

    public int Y2;

    /// <summary>Tick by which the task must be done; 0 = none.</summary>
    public int Deadline;

    /// <summary>0 queued, 1 active, 2 done, 3 failed.</summary>
    public int Status;

    /// <summary>Planner step inside the task (attack: 0 assemble, 1 prep fires, 2 assault).</summary>
    public int Step;

    public int StepTick;

    public int StagingX;

    public int StagingY;

    /// <summary>MEC-05 battle-planner preparation in hundredths, 0..10000: grows while the plan is left to its commander, decays with
    /// every direct order to one of its units, and scales the plan's hit-chance bonus.</summary>
    public int Prep;

    public int FireRequestTick;

    public int Escalated;
}

/// <summary>An inter-commander support request (CMD-05).</summary>
public struct SupportRequest
{
    public int Id;

    public int FromSeat;

    public int ToSeat;

    /// <summary>1 fire mission, 2 CAS, 3 resupply, 4 armor support.</summary>
    public int Kind;

    public int X;

    public int Y;

    public int Priority;

    /// <summary>0 pending approval, 1 approved, 2 denied, 3 done.</summary>
    public int Status;

    public int Tick;

    /// <summary>Unit the request concerns (resupply), or -1.</summary>
    public int Unit;

    /// <summary>Unit assigned to fulfil it (truck), or -1.</summary>
    public int Assignee;

    /// <summary>CAS: tick the assigned aircraft came on station (0 = not yet).</summary>
    public int OnStationTick;
}

/// <summary>SUP-03: an ammunition dump (map object): resupplies its team's units and reloads trucks; HE can destroy it.</summary>
public struct AmmoDump
{
    public int X;

    public int Y;

    public int Team;

    public int SupplyLeft;

    public int Hp;
}

public struct Objective
{
    public int X;

    public int Y;

    /// <summary>Owning team, -1 neutral.</summary>
    public int Owner;
}

public struct SmokeCloud
{
    public int Active;

    public Fp X;

    public Fp Y;

    public int RadiusM;

    public int StartTick;

    public int ExpiresTick;
}

public struct BattleEvent
{
    public int Tick;

    public int Kind;

    public int A;

    public int B;

    public int Value;

    /// <summary>Extra field: mount slot (relative to the shooter's MountStart) for Fired/Hit/NonPen/Miss.</summary>
    public int C;

    public const int Spotted = 1;

    public const int Lost = 2;

    public const int Fired = 3;

    public const int Hit = 4;

    public const int Killed = 5;

    public const int NoPath = 6;

    public const int Arrived = 7;

    public const int ChunkDestroyed = 8;

    public const int ChunkFell = 9;

    public const int CrushDamage = 10;

    public const int Critical = 11;

    public const int NonPen = 12;

    public const int Miss = 13;

    public const int MoraleChange = 14;

    public const int Surrendered = 15;

    public const int Casualty = 16;

    public const int Resupplied = 17;

    public const int OutOfFuel = 18;

    public const int Rtb = 19;

    public const int Returned = 20;

    public const int Deployed = 21;

    public const int PhaseChange = 22;

    public const int DeployRefused = 23;

    /// <summary>A, B = impact x, y (m); C = weapon index; Value = mission kind.</summary>
    public const int Impact = 24;

    public const int MissionStarted = 25;

    public const int MissionRefused = 26;

    /// <summary>A = battery unit id located, B = locating team, C/Value = fix x/y.</summary>
    public const int BatteryLocated = 27;

    /// <summary>A = seat, B = committed units, C = fit units available, Value = 1 if preparatory fires landed first.</summary>
    public const int AssaultStarted = 28;

    /// <summary>A = unit id, B = seat.</summary>
    public const int Withdraw = 29;

    /// <summary>A = seat, B = task index, Value = task kind.</summary>
    public const int TaskDone = 30;

    public const int TaskFailed = 31;

    /// <summary>A = seat, Value = escalation kind (1 attack failing, 2 cannot hold).</summary>
    public const int Escalation = 32;

    /// <summary>A = request id, B = from seat, C = to seat, Value = kind.</summary>
    public const int RequestMade = 33;

    public const int RequestApproved = 34;

    public const int RequestDenied = 35;

    /// <summary>A = objective index, B = new owner team.</summary>
    public const int ObjectiveTaken = 36;

    public const int GameOver = 37;

    /// <summary>A = unit id: first time its hp fell below command.damaged_hp_pct of full.</summary>
    public const int Damaged = 38;

    /// <summary>A = request id, B = aircraft unit id: an air seat dispatched an aircraft to a CAS request.</summary>
    public const int CasDispatched = 39;

    /// <summary>A = request id, B = aircraft unit id: the CAS aircraft is on station over the request point.</summary>
    public const int CasOnStation = 40;

    /// <summary>A = player, B = aircraft unit id: the player took control of an aircraft; the XO has command (FLY-01).</summary>
    public const int XoTookCommand = 41;

    /// <summary>A = player, B = aircraft unit id, Value = reason (1 Bingo, 2 Winchester, 3 ejected, 4 landed, 5 manual): control returned (FLY-02).</summary>
    public const int ControlReturned = 42;

    /// <summary>A = aircraft unit id: a flown aircraft fell below its stall speed (FLY-03).</summary>
    public const int StallWarning = 43;

    /// <summary>A = unit id, Value = new veterancy: promoted by kills (MEC-04).</summary>
    public const int Promoted = 44;

    /// <summary>A = unit id: a retreating unit reached home and was reinforced (MEC-04).</summary>
    public const int Reinforced = 45;

    /// <summary>A = seat, Value = remaining preparation: a direct order to one of a plan's units cost preparation (MEC-05).</summary>
    public const int PrepLost = 46;

    /// <summary>A = player, B = option, C = arrival tick: off-map support called and paid for (MEC-03).</summary>
    public const int SupportCalled = 47;

    /// <summary>A = player, B = option: the called support arrives (response delay over).</summary>
    public const int SupportArrived = 48;

    /// <summary>A = player, B = option, Value = reason (1 wrong side, 2 short of SP, 3 no charges left, 5 wrong era).</summary>
    public const int SupportRefused = 49;

    /// <summary>A = player, B = victim id: a kill made possible by another unit's spotting earned the assist SP (MEC-03).</summary>
    public const int SpotAssist = 50;

    /// <summary>MEC-06: A = player, B/C/Value = offered task-force indices (-1 none).</summary>
    public const int FieldOffered = 51;

    /// <summary>MEC-06: A = player, B = task-force index, Value = units arrived.</summary>
    public const int FieldTaken = 52;

    /// <summary>SUP-03: A = repaired unit id, B = recovery vehicle id, Value = critical flag fixed (0 = back to full hp).</summary>
    public const int Repaired = 53;

    /// <summary>SUP-03: A = unit id that struck a mine, B/C = cell x/y (metres), Value = density left.</summary>
    public const int MineStrike = 54;

    /// <summary>SUP-03: A = player, B = engineer support type, C/Value = aim point x/y.</summary>
    public const int EngineerWork = 55;

    /// <summary>SUP-03: A = dump index, B = team that lost it.</summary>
    public const int DumpDestroyed = 56;

    /// <summary>SUP-03: A = squad id, B = medevac aircraft id, Value = members now.</summary>
    public const int Medevaced = 57;

    /// <summary>MEC-07: A = new operation phase, B = event card index, C = phase winner, Value = time-of-day step.</summary>
    public const int OperationAdvanced = 58;

    /// <summary>FLY-06: a player-flown aircraft is under fire. A = aircraft id, B = shooter id, Value = 1 radar-directed
    /// gunfire, 2 missile launch (RWR / launch warning for the pilot).</summary>
    public const int ThreatWarning = 59;
}

public sealed class WorldState
{
    public int Tick;

    public ulong Seed;

    public ulong Rng;

    public int Weather;

    /// <summary>SIM-11: 1 when the battle's weather grounds aircraft (rules.weather_air_ops), set at build.</summary>
    public int AirGrounded;

    /// <summary>GS-04: highest nuclear level the grand-strategy escalation has released for this battle (0 none,
    /// 1 tactical). Tactical support cards are refused below 1.</summary>
    public int NuclearRelease;

    /// <summary>MODE-01 victory type: 0 conquest (objective points), 1 annihilation (also won by leaving the enemy
    /// with no ground combat units).</summary>
    public int VictoryType;

    /// <summary>MEC-07: time-of-day step (0 dawn, 1 day, 2 dusk, 3 night). Default day.</summary>
    public int TimeOfDay = 1;

    /// <summary>MEC-07: phase of a multi-phase operation (0 = first) and the tick it started (economy phases and the
    /// time cap count from it).</summary>
    public int OperationPhase;

    public int OperationStartTick;

    /// <summary>MEC-07: event cards drawn between phases, in order.</summary>
    public List<int> OperationEvents = [];

    /// <summary>MEC-07 event deck (data; not hashed).</summary>
    public SixActual.Sim.Battle.OperationEventDef[] EventDefs = [];

    public TerrainGrid Terrain = new();

    public BattleUnitDef[] Defs = [];

    public WeaponDef[] Weapons = [];

    public DeckDef[] Decks = [];

    /// <summary>Rounds left per mounted weapon; unit u's mounts start at Units[u].MountStart.</summary>
    public int[] MountAmmo = [];

    /// <summary>Ticks until each mount can fire again.</summary>
    public int[] MountReload = [];

    public UnitState[] Units = [];

    public SmokeCloud[] Smoke = new SmokeCloud[64];

    /// <summary>Illumination flares: reuse the smoke struct (position, radius, start, expiry).</summary>
    public SmokeCloud[] Flares = new SmokeCloud[32];

    public List<BatteryFix> Fixes = [];

    /// <summary>Pending counter-battery detections: (due tick, battery index, shot x, shot y).</summary>
    public List<(int Due, int Battery, int X, int Y)> CbPending = [];

    public PlayerState[] Players = [];

    /// <summary>Cards left per player per deck card.</summary>
    public int[] CardStock = [];

    public int Phase;

    /// <summary>Owner team per frontline cell (0 or 1; -1 contested/none).</summary>
    public sbyte[] Territory = [];

    public int FrontCells;

    /// <summary>Per unit: 1 when cut off from its team's rear (MEC-02).</summary>
    public byte[] Encircled = [];

    public int NextUnitId;

    public FireMission[] Missions = [];

    public Shell[] Shells = [];

    public SeatState[] Seats = [];

    public List<CommandTask> Tasks = [];

    public List<SupportRequest> Requests = [];

    public int NextRequestId;

    public Objective[] Objectives = [];

    /// <summary>Victory points per team (0, 1).</summary>
    public int[] Score = new int[2];

    /// <summary>Score remainder in 1 / (ticks per minute) of a point.</summary>
    public int[] ScoreMilli = new int[2];

    /// <summary>-1 running, 0/1 team won, 2 draw.</summary>
    public int Winner = -1;

    /// <summary>Per player: 1 when the XO has command (player flying an aircraft, or an AI player).</summary>
    public int[] XoActive = [];

    /// <summary>FLY-01: each player's XoActive before Take Control, restored when control returns.</summary>
    public int[] XoRestore = [];

    public Building[] Buildings = [];

    /// <summary>Off-map support catalogue (data/support.json) and per player x option charges and recharge-ready ticks.</summary>
    /// <summary>Battle era ("ww2", "coldwar", "modern"): support options of another era are refused (SUP-05).</summary>
    public string Era = "coldwar";

    public SixActual.Sim.Battle.SupportDef[] SupportDefs = [];

    /// <summary>MEC-06 task-force pools (data; not hashed).</summary>
    public SixActual.Sim.Battle.FieldRequestDef[] FieldRequestDefs = [];

    /// <summary>MEC-06: 1 when the match uses Field Requests (campaign default, skirmish toggle).</summary>
    public int FieldRequestsOn;

    /// <summary>Open offers, 3 per player (task-force index or -1).</summary>
    public int[] FieldOffers = [];

    /// <summary>Tick each player's current offer was made.</summary>
    public int[] FieldOfferTick = [];

    /// <summary>SUP-03 ammunition dumps.</summary>
    public AmmoDump[] Dumps = [];

    public int[] SupportCharges = [];

    public int[] SupportReady = [];

    /// <summary>Called support on its way or delivering (strike runs deliver over several ticks).</summary>
    public List<PendingSupport> PendingSupport = [];

    /// <summary>Active reveals (recon flights, AWACS): enemy units inside are spotted by the player.</summary>
    public List<Reveal> Reveals = [];

    /// <summary>SUP-04 active and expired EW zones (expired ones are ignored).</summary>
    public List<EwZone> EwZones = [];

    /// <summary>MEC-04: crater positions (metres) left by heavy HE impacts; a ring of rules.crater_max.</summary>
    public int[] CraterX = [];

    public int[] CraterY = [];

    public int CraterCount;

    /// <summary>Per-tick cache (not state): indices of destroyed ground vehicles, rebuilt from Units by Combat.Step.</summary>
    public List<int> Wrecks = [];

    /// <summary>Hit points per building chunk; 0 = gone. Indexed by Building.ChunkStart + (floor * Ny + iy) * Nx + ix.</summary>
    public int[] ChunkHp = [];

    /// <summary>Tick at which an unsupported chunk falls; 0 = not falling.</summary>
    public int[] ChunkFallTick = [];

    public int StructureDirty;

    public List<BattleEvent> Events = [];

    /// <summary>Per-unit waypoint list in nav-cell indices, parallel to Units.</summary>
    public int[][] Paths = [];

    /// <summary>Per movement class (0 tracked, 1 wheeled, 2 infantry): cost per nav cell, 0 = impassable.</summary>
    public int[][] NavCost = [];

    /// <summary>Coarse cost grid per movement class (Navigation.CoarseFactor nav cells per side).</summary>
    public int[][] NavCoarse = [];

    public int NavCells;

    public int NavCellM;

    public int NavVersion = -1;
}
