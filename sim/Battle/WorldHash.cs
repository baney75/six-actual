using System.Security.Cryptography;
using System.Text;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>SHA-256 over every field of the battle world that affects future ticks (desync detection, replays).</summary>
public static class WorldHash
{
    public static string Of(WorldState w)
    {
        ArgumentNullException.ThrowIfNull(w);
        using var buffer = new MemoryStream();
        using (var b = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            b.Write(w.Tick);
            b.Write(w.Seed);
            b.Write(w.Rng);
            b.Write(w.Weather);
            b.Write(w.AirGrounded);
            b.Write(w.NuclearRelease);
            b.Write(w.TimeOfDay);
            b.Write(w.VictoryType);
            b.Write(w.OperationPhase);
            b.Write(w.OperationStartTick);
            foreach (var e in w.OperationEvents)
            {
                b.Write(e);
            }

            b.Write(w.Terrain.Version);
            foreach (var h in w.Terrain.HeightDm)
            {
                b.Write(h);
            }

            b.Write(w.Terrain.Forest);
            b.Write(w.Terrain.Road);
            b.Write(w.Terrain.Hedge);
            b.Write(w.Terrain.Blocked);
            b.Write(w.Terrain.Obstacle);
            b.Write(w.Terrain.Fort);
            b.Write(w.Terrain.MineDensity);
            b.Write(w.Terrain.MineTeam);
            foreach (var r in w.Terrain.RubbleDm)
            {
                b.Write(r);
            }

            foreach (var hp in w.ChunkHp)
            {
                b.Write(hp);
            }

            foreach (var ft in w.ChunkFallTick)
            {
                b.Write(ft);
            }
            foreach (var u in w.Units)
            {
                b.Write(u.Id);
                b.Write(u.Def);
                b.Write(u.Player);
                b.Write(u.Alive);
                b.Write(u.X.Raw);
                b.Write(u.Y.Raw);
                b.Write(u.Z.Raw);
                b.Write(u.Heading);
                b.Write(u.Hp);
                b.Write(u.Ammo);
                b.Write(u.Fuel);
                b.Write(u.Morale);
                b.Write(u.Suppression);
                b.Write(u.Flags);
                b.Write(u.SpottedBy);
                b.Write(u.RevealUntilTick);
                b.Write(u.HasGoal);
                b.Write(u.GoalX.Raw);
                b.Write(u.GoalY.Raw);
                b.Write(u.PathStep);
                b.Write(u.Members);
                b.Write(u.MoraleState);
                b.Write(u.RoutedSinceTick);
                b.Write(u.Veterancy);
                b.Write(u.Kills);
                b.Write(u.Roe);
                b.Write(u.LastHitByTick);
                b.Write(u.SupplyLeft);
                b.Write(u.MetresSinceFuel);
                b.Write(u.OffMapUntil);
                b.Write(u.SortieDone);
                b.Write(u.Flown);
                b.Write(u.RepairTicks);
                b.Write(u.Throttle);
                b.Write(u.StickTurn);
                b.Write(u.StickClimb);
                b.Write(u.AirspeedMmS);
                b.Write(u.SinGammaPm);
                b.Write(u.Stalled);
                b.Write(u.Retreating);
                b.Write(u.Cost);
                b.Write(u.LastAttacker);
                b.Write(u.NextScanTick);
                b.Write(u.Seat);
                b.Write(u.Withdrawing);
                b.Write(u.AskedResupply);
                b.Write(u.WasDamaged);

                // Mutable fields that steer later ticks (D073 review): an unhashed drift here would go unseen until it
                // changed something hashed.
                b.Write(u.FuelMilli);
                b.Write(u.HomeX);
                b.Write(u.HomeY);
                b.Write(u.NoPath);
                b.Write(u.LastProgressTick);
                b.Write(u.MovedThisTick);
                b.Write(u.IsHq);
            }

            foreach (var s in w.Smoke)
            {
                b.Write(s.Active);
                b.Write(s.X.Raw);
                b.Write(s.Y.Raw);
                b.Write(s.RadiusM);
                b.Write(s.StartTick);
                b.Write(s.ExpiresTick);
            }

            foreach (var path in w.Paths)
            {
                b.Write(path?.Length ?? -1);
                foreach (var c in path ?? [])
                {
                    b.Write(c);
                }
            }

            foreach (var a in w.MountAmmo)
            {
                b.Write(a);
            }

            foreach (var rl in w.MountReload)
            {
                b.Write(rl);
            }

            foreach (var m in w.Missions)
            {
                b.Write(m.Active);
                b.Write(m.TargetX);
                b.Write(m.TargetY);
                b.Write(m.RoundsLeft);
                b.Write(m.Kind);
                b.Write(m.NextShotTick);
                b.Write(m.Battery);
                b.Write(m.Mount);
            }

            foreach (var sh in w.Shells)
            {
                b.Write(sh.Active);
                b.Write(sh.LandTick);
                b.Write(sh.X);
                b.Write(sh.Y);
                b.Write(sh.Weapon);
                b.Write(sh.Kind);
                b.Write(sh.Shooter);
            }

            foreach (var f in w.Flares)
            {
                b.Write(f.Active);
                b.Write(f.X.Raw);
                b.Write(f.Y.Raw);
                b.Write(f.ExpiresTick);
                b.Write(f.RadiusM);
                b.Write(f.StartTick);
            }

            foreach (var f in w.Fixes)
            {
                b.Write(f.Active);
                b.Write(f.Tick);
                b.Write(f.Team);
                b.Write(f.Battery);
                b.Write(f.X);
                b.Write(f.Y);
            }

            b.Write(w.CbPending.Count);
            foreach (var st in w.Seats)
            {
                b.Write(st.Aggression);
                b.Write(st.CasualtyTolerance);
                b.Write(st.Roe);
                b.Write(st.ApprovalMode);
                b.Write(st.PriorityKind);
            }

            foreach (var t in w.Tasks)
            {
                b.Write(t.Seat);
                b.Write(t.Kind);
                b.Write(t.Status);
                b.Write(t.Step);
                b.Write(t.StepTick);
                b.Write(t.StagingX);
                b.Write(t.StagingY);
                b.Write(t.Prep);
            }

            foreach (var q in w.Requests)
            {
                b.Write(q.Id);
                b.Write(q.Status);
                b.Write(q.Assignee);
                b.Write(q.Priority);
                b.Write(q.OnStationTick);
            }

            b.Write(w.CraterCount);
            for (var c = 0; c < Math.Min(w.CraterCount, w.CraterX.Length); c++)
            {
                b.Write(w.CraterX[c]);
                b.Write(w.CraterY[c]);
            }

            b.Write(w.Era);
            b.Write(w.FieldRequestsOn);
            foreach (var d in w.Dumps)
            {
                b.Write(d.X);
                b.Write(d.Y);
                b.Write(d.Team);
                b.Write(d.SupplyLeft);
                b.Write(d.Hp);
            }

            foreach (var x in w.FieldOffers)
            {
                b.Write(x);
            }

            foreach (var x in w.FieldOfferTick)
            {
                b.Write(x);
            }

            foreach (var x in w.SupportCharges)
            {
                b.Write(x);
            }

            foreach (var x in w.SupportReady)
            {
                b.Write(x);
            }

            foreach (var ps in w.PendingSupport)
            {
                b.Write(ps.Def);
                b.Write(ps.Player);
                b.Write(ps.X);
                b.Write(ps.Y);
                b.Write(ps.Arrive);
                b.Write(ps.Done);
            }

            foreach (var ez in w.EwZones)
            {
                b.Write(ez.Team);
                b.Write(ez.Kind);
                b.Write(ez.X);
                b.Write(ez.Y);
                b.Write(ez.RadiusM);
                b.Write(ez.Until);
            }

            foreach (var rv in w.Reveals)
            {
                b.Write(rv.Player);
                b.Write(rv.X);
                b.Write(rv.Y);
                b.Write(rv.RadiusM);
                b.Write(rv.Until);
                b.Write(rv.AirOnly);
            }

            foreach (var x in w.XoActive)
            {
                b.Write(x);
            }

            foreach (var x in w.XoRestore)
            {
                b.Write(x);
            }

            foreach (var o in w.Objectives)
            {
                b.Write(o.Owner);
            }

            b.Write(w.Score[0]);
            b.Write(w.Score[1]);
            b.Write(w.Winner);
            b.Write(w.Phase);
            foreach (var t in w.Territory)
            {
                b.Write(t);
            }

            b.Write(w.Encircled);
            b.Write(w.NextUnitId);
            foreach (var pl in w.Players)
            {
                b.Write(pl.Dp);
                b.Write(pl.DpMilli);
                b.Write(pl.Sp);
                b.Write(pl.Profile);
                b.Write(pl.SupplyDelivered);
                b.Write(pl.DpSpent);
                b.Write(pl.DpEarned);
                b.Write(pl.Team);
                b.Write(pl.Deck);
                b.Write(pl.StockStart);
                b.Write(pl.SpawnX);
                b.Write(pl.SpawnY);
            }

            foreach (var c in w.CardStock)
            {
                b.Write(c);
            }

            b.Write(w.Events.Count);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }
}
