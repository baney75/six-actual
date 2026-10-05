using System.Text.Json;

namespace SixActual.Sim;

public static class UnitCatalog
{
    public static UnitDef[] Load(string path)
    {
        var reader = new Utf8JsonReader(File.ReadAllBytes(path));
        var units = new List<UnitDef>();
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "units")
            {
                continue;
            }

            reader.Read();
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new InvalidDataException("units must be an array.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new InvalidDataException("Each unit must be an object.");
                }

                units.Add(ReadUnit(ref reader));
            }

            break;
        }

        if (units.Count == 0)
        {
            throw new InvalidDataException("Unit catalog is empty.");
        }

        return units.ToArray();
    }

    public static int IndexOf(UnitDef[] defs, string id)
    {
        for (var i = 0; i < defs.Length; i++)
        {
            if (string.Equals(defs[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidDataException($"Missing unit {id}.");
    }

    static UnitDef ReadUnit(ref Utf8JsonReader reader)
    {
        var fields = new Dictionary<string, int>(StringComparer.Ordinal);
        string id = string.Empty;
        string era = string.Empty;
        string faction = string.Empty;
        string kind = string.Empty;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new InvalidDataException("Expected a unit property.");
            }

            var name = reader.GetString() ?? string.Empty;
            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString() ?? string.Empty;
                switch (name)
                {
                    case "id": id = text; break;
                    case "era": era = text; break;
                    case "faction": faction = text; break;
                    case "kind": kind = text; break;
                    default: throw new InvalidDataException($"Unexpected string {name}.");
                }
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                if (!reader.TryGetInt32(out var number))
                {
                    throw new InvalidDataException($"{name} must be an integer.");
                }

                fields[name] = number;
            }
            else
            {
                throw new InvalidDataException($"{name} has an unsupported value.");
            }
        }

        return new UnitDef
        {
            Id = id,
            Era = era,
            Faction = faction,
            Year = Required(fields, "year"),
            Kind = UnitDef.ParseKind(kind),
            LengthMm = Required(fields, "length_mm"),
            WidthMm = Required(fields, "width_mm"),
            HeightMm = Required(fields, "height_mm"),
            SpeedMmS = Required(fields, "speed_mm_s"),
            Hp = Required(fields, "hp"),
            ArmorFront = Required(fields, "armor_front"),
            ArmorSide = Required(fields, "armor_side"),
            ArmorRear = Required(fields, "armor_rear"),
            ArmorTop = Required(fields, "armor_top"),
            PenMm = Required(fields, "pen_mm"),
            PenDropPerKm = Required(fields, "pen_drop_per_km"),
            RangeM = Required(fields, "range_m"),
            ReloadTicks = Required(fields, "reload_ticks"),
            OpticsM = Required(fields, "optics_m"),
            Stealth = Required(fields, "stealth"),
            Ammo = Required(fields, "ammo"),
            Fuel = Required(fields, "fuel"),
        };
    }

    static int Required(Dictionary<string, int> fields, string name)
    {
        if (!fields.TryGetValue(name, out var value))
        {
            throw new InvalidDataException($"Missing {name}.");
        }

        return value;
    }
}
