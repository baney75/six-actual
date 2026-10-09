using System.Collections.Generic;
using System.Linq;
using SixActual.Sim.Battle;

/// <summary>
/// MEC-11 card read-out from data: a role icon (a text glyph until the icon set is drawn), the effective-range bar
/// (the longest-reaching weapon) and a "use it for" line keyed by the unit's research role (roster "kind").
/// </summary>
public readonly record struct CardInfo(string Icon, string Role, int RangeM, string UseFor)
{
    /// <summary>The range bar is full at 4 km, the reach of the longest direct-fire missiles.</summary>
    public const int RangeScaleM = 4000;

    static readonly Dictionary<string, (string Icon, string Role, string UseFor)> ByRole = new()
    {
        ["tank"] = ("◆", "Tank", "Breaking enemy armor and leading the assault"),
        ["td"] = ("◇", "Tank destroyer", "Killing tanks from concealed positions"),
        ["ifv"] = ("▲", "Infantry fighting vehicle", "Carrying infantry and fighting beside them"),
        ["apc"] = ("△", "Carrier", "Moving infantry fast under armor"),
        ["recon"] = ("◎", "Reconnaissance", "Finding the enemy first and calling fire"),
        ["atgm_vehicle"] = ("➶", "Missile carrier", "Killing tanks at long range from cover"),
        ["infantry"] = ("●", "Infantry", "Taking and holding objectives, towns and woods"),
        ["at_team"] = ("➶", "Anti-tank team", "Ambushing armor from cover"),
        ["manpads"] = ("✈̸", "Air-defense team", "Shooting down low helicopters and jets"),
        ["mortar"] = ("✚", "Mortar", "Quick indirect fire and smoke for the front line"),
        ["mortar_team"] = ("✚", "Mortar team", "Quick indirect fire and smoke for the front line"),
        ["towed_gun"] = ("✚", "Towed gun", "Fire missions from the rear"),
        ["spg"] = ("✚", "Self-propelled gun", "Fire missions, then moving before counter-battery"),
        ["mlrs"] = ("✚", "Rocket artillery", "Saturating an area in one salvo"),
        ["spaag"] = ("✈̸", "AA gun", "Shooting down helicopters over your units"),
        ["sam"] = ("✈̸", "SAM", "Denying the sky to enemy aircraft"),
        ["attack_helo"] = ("✈", "Attack helicopter", "Killing armor from behind terrain"),
        ["recon_helo"] = ("✈", "Scout helicopter", "Spotting for attack helicopters and artillery"),
        ["transport_helo"] = ("✈", "Transport helicopter", "Moving troops and supplies fast"),
        ["attack_jet"] = ("✈", "Attack jet", "Close air support against armor and troops"),
        ["strike_jet"] = ("✈", "Strike jet", "Deep strikes on artillery, supply and air defense"),
        ["multirole_jet"] = ("✈", "Multirole jet", "Air cover and strikes"),
        ["fighter"] = ("✈", "Fighter", "Air superiority: hunting enemy aircraft"),
        ["supply"] = ("■", "Supply", "Rearming and refueling units near the front"),
        ["recovery"] = ("■", "Recovery", "Repairing damaged vehicles near the front"),
        ["command"] = ("★", "Command", "Command post that keeps your commanders in contact"),
    };

    public static CardInfo Of(BattleUnitDef def, WeaponDef[] weapons)
    {
        var range = def.Weapons.Length == 0 ? 0 : def.Weapons.Max(m => weapons[m.Weapon].RangeMaxM);
        var r = ByRole.TryGetValue(def.Role, out var v) ? v : (Icon: "·", Role: def.Role, UseFor: "Support role");
        return new CardInfo(r.Icon, r.Role, range, r.UseFor);
    }
}
