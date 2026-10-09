namespace SixActual.Sim;

public struct UnitDef
{
    public string Id;

    public string Era;

    public string Faction;

    public int Year;

    public int Kind;

    public int LengthMm;

    public int WidthMm;

    public int HeightMm;

    public int SpeedMmS;

    public int Hp;

    public int ArmorFront;

    public int ArmorSide;

    public int ArmorRear;

    public int ArmorTop;

    public int PenMm;

    public int PenDropPerKm;

    public int RangeM;

    public int ReloadTicks;

    public int OpticsM;

    public int Stealth;

    public int Ammo;

    public int Fuel;

    public const int KindInfantry = 1;

    public const int KindTank = 2;

    public const int KindIfv = 3;

    public const int KindArtillery = 4;

    public const int KindAa = 5;

    public const int KindAir = 6;

    public const int KindSupply = 7;

    public const int KindCommand = 8;

    public bool IsAir => Kind == KindAir;

    public static int ParseKind(string kind) => kind switch
    {
        "infantry" => KindInfantry,
        "tank" => KindTank,
        "ifv" => KindIfv,
        "artillery" => KindArtillery,
        "aa" => KindAa,
        "air" => KindAir,
        "supply" => KindSupply,
        "command" => KindCommand,
        _ => throw new InvalidDataException($"Unknown unit kind {kind}."),
    };
}
