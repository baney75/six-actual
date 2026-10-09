namespace SixActual.Sim.Tests;

public class CampaignTests
{
    [Fact]
    public void Six_wins_finish_a_campaign_and_grant_experience()
    {
        var state = Campaign.Start(1);
        for (var i = 0; i < Campaign.OperationsPerEra; i++)
        {
            Assert.False(Campaign.IsFinished(state));
            Campaign.ApplyResult(state, playerWon: true);
        }

        Assert.True(Campaign.IsFinished(state));
        Assert.Equal(6, state.Wins);
        Assert.Equal(60, state.Xp);
        Assert.Equal(0, state.Wounded);
    }

    [Fact]
    public void Three_losses_wound_the_commander_and_end_early()
    {
        var state = Campaign.Start(0);
        Campaign.ApplyResult(state, false);
        Campaign.ApplyResult(state, false);
        Campaign.ApplyResult(state, false);
        Assert.True(Campaign.IsFinished(state));
        Assert.Equal(1, state.Wounded);
        Assert.Equal(3, state.Losses);
        var ops = state.Operation;
        Campaign.ApplyResult(state, true);
        Assert.Equal(ops, state.Operation);
    }

    [Fact]
    public void Each_era_has_six_operation_names()
    {
        for (var era = 0; era < Campaign.Eras.Length; era++)
        {
            var state = Campaign.Start(era);
            var names = new HashSet<string>();
            for (var i = 0; i < Campaign.OperationsPerEra; i++)
            {
                names.Add(Campaign.OperationName(state));
                state.Operation++;
            }

            Assert.Equal(Campaign.OperationsPerEra, names.Count);
        }
    }

    [Fact]
    public void A_war_runs_to_an_owner_and_peace_does_not_take_land()
    {
        var war = Strategy.Run(24);
        Assert.True(war.Winner >= 0);
        var peace = Strategy.Start();
        Strategy.SetPeace(peace);
        var before = peace.Owner.ToArray();
        Strategy.EndTurn(peace);
        Assert.Equal(before, peace.Owner);
    }

    [Fact]
    public void Tension_spends_the_nuclear_weapon_on_enemy_industry()
    {
        var state = Strategy.Start();
        state.Tension = 4;
        var before = 0;
        for (var i = 0; i < Strategy.ProvinceCount; i++)
        {
            if (state.Owner[i] == 1)
            {
                before += state.Industry[i];
            }
        }

        Strategy.EndTurn(state);
        var after = 0;
        for (var i = 0; i < Strategy.ProvinceCount; i++)
        {
            if (state.Owner[i] == 1)
            {
                after += state.Industry[i];
            }
        }

        Assert.Equal(0, state.Nukes);
        Assert.True(after < before);
        Assert.True(state.Tension > 4);
    }

    [Fact]
    public void Unsupported_chunks_fall_and_damage_what_they_hit()
    {
        var column = new[] { 0, 1, 1 };
        var damage = Collapse.Step(column);
        Assert.Equal(20, damage);
        Assert.Equal(new[] { 0, 0, 0 }, column);
        Assert.Equal(0, Collapse.Step(column));
    }

    [Fact]
    public void Campaign_state_survives_save_and_load()
    {
        // CMD-08: XP and wounds persist through a save/load round trip, and play continues identically.
        var a = Campaign.Start(1);
        Campaign.ApplyResult(a, playerWon: true);
        Campaign.ApplyResult(a, playerWon: false);
        var json = Campaign.Save(a);
        var b = Campaign.Load(json);
        Assert.Equal(json, Campaign.Save(b));
        Assert.Equal(12, b.Xp);
        Assert.Equal(1, b.Wounded);
        Campaign.ApplyResult(a, playerWon: true);
        Campaign.ApplyResult(b, playerWon: true);
        Assert.Equal(Campaign.Save(a), Campaign.Save(b));
    }
}
