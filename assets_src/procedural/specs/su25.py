"""Su-25 Frogfoot (1989, Soviet Frontal Aviation) for assets_src/procedural/aircraft.py.

Blender axes: +Y nose, +Z up, metres, gear down. Sourced (data/assets/su25.json): length 15.53 (with probes),
span 14.36, height 4.8, 10 underwing pylons. Plan layout from the public-domain two-view silhouette top mask,
positions from the probe tips: twin nose probes 0-1.1 m, body nose at 1.0 m, forward fuselage 1.08 m wide,
wing LE 6.2 m at the root and 8.6 m at the tip with a straight TE, tailplane 12.3-14.3 m. At this scale the
drawing's span is 7% short of the sourced 14.36 m, which the model keeps. Front-view widths from the front mask
(nacelle block +-1.29 m). The drawing has no side view: the side profile, canopy, sit height and nacelle form
come from the side photos via review_1..3.md (about +-0.1 m). Wing chord trimmed to the sourced 33.7 m2.
"""
N = 7.765  # probe tips at +Y = length / 2
DZ = 0.18  # sit height: belly at the nose gear about 1.05 m (photos); fin trimmed to hold 4.8 m overall
PYLONS = (2.00, 2.78, 3.50, 4.25, 5.03)  # front view store stations, 5 per side = 10


def y(pos):
    return N - pos


def wing_le(x):
    return 6.2 + 2.4 * (x - 0.6) / 6.4


def wing_z(x):
    return 2.0 - 0.40 * (x - 0.6) / 6.4


SPEC = {
    "paint": (0.47, 0.44, 0.32),
    # (y, half width, z centre, half height)
    "fuselage": [
        (y(1.00), 0.12, 1.45, 0.10),   # chiselled nose tip 0.5 m above the belly line
        (y(1.60), 0.38, 1.45, 0.30),
        (y(2.50), 0.53, 1.50, 0.55),
        (y(3.30), 0.54, 1.55, 0.65),   # windscreen base
        (y(4.50), 0.54, 1.58, 0.72),
        (y(5.50), 0.56, 1.60, 0.75),   # 1.5 m deep mid fuselage
        (y(7.00), 0.60, 1.60, 0.75),
        (y(10.0), 0.55, 1.62, 0.70),
        (y(11.5), 0.38, 1.75, 0.50),
        (y(12.5), 0.32, 1.85, 0.40),
        (y(14.5), 0.22, 1.88, 0.28),
        (y(15.0), 0.22, 1.88, 0.25),
        (y(15.53), 0.08, 1.88, 0.10),
    ],
    "cockpit": (0.0, y(4.0), 2.1),
    # Single-seat hood: steep framed windscreen from 2.17 m behind the nose tip, crown 0.65 m above the sill,
    # 0.8 m wide at the sill tapering to 0.45 m at the crown, rear at 4.0 m.
    "canopy": {"type": "hood", "sill_z": 2.20, "crown": 0.56,
               "sections": [(y(3.17), 0.36, 0.04), (y(3.62), 0.40, 0.65), (y(4.60), 0.40, 0.65), (y(5.00), 0.38, 0.52)]},
    "fairings": [
        # Windscreen frame bow and the rear hump fairing the hood into the spine.
        {"name": "CanopyFrame", "sill_z": 2.19, "crown": 0.56, "dark": True,
         "sections": [(y(3.58), 0.42, 0.67), (y(3.66), 0.42, 0.67)]},
        {"name": "SpineHump", "sill_z": 2.15, "crown": 0.56,
         "sections": [(y(4.95), 0.40, 0.58), (y(5.60), 0.36, 0.42), (y(6.60), 0.28, 0.20)]},
        # Smooth fairing between the nacelles and the fuselage aft of the intakes (no stepped boxes).
        {"name": "NacelleFairing", "sill_z": 0.84, "crown": 0.80,
         "sections": [(y(6.40), 0.60, 0.70), (y(7.20), 0.95, 0.88), (y(10.0), 0.95, 0.88), (y(10.9), 0.55, 0.70)]},
    ],
    "surfaces": [
        # Shoulder wing, about 20 deg LE sweep, slight anhedral (front mask: root z 2.0, tip z 1.6).
        {"name": "Wing", "root": (0.60, y(6.2), wing_z(0.6), 3.60), "tip": (7.00, y(8.6), wing_z(7.0), 1.30), "thickness_pct": 12,
         "controls": [{"name": "Flap", "span": (0.05, 0.60, 0.28)}, {"name": "Aileron", "span": (0.62, 0.96, 0.28)}]},
        {"name": "Tailplane", "root": (0.35, y(12.3), 2.35, 2.00), "tip": (2.27, y(13.4), 2.35, 0.85), "thickness_pct": 9,
         "controls": [{"name": "Elevator", "span": (0.05, 0.95, 0.35)}]},
        {"name": "Fin", "root": (0.0, y(11.0), 2.25, 3.20), "tip": (0.0, y(13.5), 4.80 - DZ, 1.20), "thickness_pct": 8,
         "vertical": True, "mirror": False, "controls": [{"name": "Rudder", "span": (0.05, 0.90, 0.30)}]},
    ],
    "nacelles": [
        # R-95Sh nacelles: proud oval intakes 0.68 x 0.95 m beside the cockpit with a 0.08 m splitter gap, held
        # to the wing TE, outer edge at the front mask's +-1.30 m, underside about 1.0 m above the ground.
        {"name": "Nacelle", "x": 0.96, "z": 1.32, "scale_z": 1.40, "exhaust": False,
         "sections": [(y(5.50), 0.33), (y(5.75), 0.34), (y(9.80), 0.34), (y(11.0), 0.29)]},
        # Round fixed nozzle, 0.7 m across.
        {"name": "Nozzle", "x": 0.96, "z": 1.32, "sections": [(y(10.9), 0.36), (y(11.4), 0.34)]},
        # Wingtip pods (split speed brakes), 0.48 m diameter, 2.45 m long.
        {"name": "TipPod", "x": 6.94, "z": wing_z(7.0), "exhaust": False,
         "sections": [(y(8.1), 0.06), (y(8.5), 0.24), (y(10.2), 0.24), (y(10.55), 0.08)]},
    ],
    "parts": [
        # Twin nose probes (drawing: 1.05 m long, 0.22 m either side of the centreline).
        {"name": "Probe", "size": (0.06, 1.15, 0.06), "at": (0.22, y(0.57), 1.45), "mirror": True, "dark": True},
        # Main gear doors outboard of the trailing-link legs.
        {"name": "GearDoor", "size": (0.06, 1.0, 0.60), "at": (1.33, y(8.05), 0.55), "mirror": True},
    ] + [
        {"name": f"Pylon{k}", "size": (0.10, 1.4, 0.24), "at": (x, y(wing_le(x) + 0.9), wing_z(x) - 0.27), "mirror": True}
        for k, x in enumerate(PYLONS)
    ] + [
        # GSh-30-2 twin-barrel gun fairing, port side under the cockpit.
        {"name": "Gun", "size": (0.22, 1.2, 0.20), "at": (-0.32, y(3.0), 0.95), "dark": True},
    ],
    "gear": [
        # Nose gear offset to port under the rear canopy frame; trailing-link mains 7.95 m from the tail.
        {"name": "Nose", "at": (-0.10, y(4.45), 0.88), "wheel_r": 0.33, "leg": 0.14},
        {"name": "Main", "at": (1.37, y(8.05), 0.86), "wheel_r": 0.40, "leg": 0.18, "mirror": True},
    ],
    "hardpoints": [(sx * x, y(wing_le(x) + 0.9), wing_z(x) - 0.39) for x in PYLONS for sx in (-1, 1)],
    "muzzle": (-0.32, y(2.4), 0.95),
}


def _lift(spec, dz):
    """Raise the airframe by dz on longer gear legs (wheels stay on the ground)."""
    spec["fuselage"] = [(yy, hw, zc + dz, hh) for yy, hw, zc, hh in spec["fuselage"]]
    x, yy, z = spec["cockpit"]
    spec["cockpit"] = (x, yy, z + dz)
    spec["canopy"]["sill_z"] += dz
    for fr in spec["fairings"]:
        fr["sill_z"] += dz
    for srf in spec["surfaces"]:
        srf["root"] = (srf["root"][0], srf["root"][1], srf["root"][2] + dz, srf["root"][3])
        srf["tip"] = (srf["tip"][0], srf["tip"][1], srf["tip"][2] + dz, srf["tip"][3])
    for n in spec["nacelles"]:
        n["z"] += dz
    for p in spec["parts"]:
        p["at"] = (p["at"][0], p["at"][1], p["at"][2] + dz)
    for g in spec["gear"]:
        g["at"] = (g["at"][0], g["at"][1], g["at"][2] + dz)
    spec["hardpoints"] = [(a, b, c + dz) for a, b, c in spec["hardpoints"]]
    a, b, c = spec["muzzle"]
    spec["muzzle"] = (a, b, c + dz)


_lift(SPEC, DZ)
