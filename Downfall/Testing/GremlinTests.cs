using Downfall.Core;
using Downfall.Gremlins;
using Downfall.Gremlins.Cards;
using Downfall.Gremlins.Powers;
using Downfall.Gremlins.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Testing;

internal static class GremlinTests
{
    public static Task TestVisualsRuntime() =>
        DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheGremlins>(
            "DFGRMVIS",
            GremlinMob.IsValidGremlinSpritePath);

    public static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGremlins>("DFGRM01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

        // 1. Starter GremlinKnob fired on Turn 1 start (+1 Energy -> 4, +1 Draw -> 6, Swap executed, Disabled).
        AutoTestAssert.Equal(75, player.Creature.MaxHp, "TheGremlins starts with 75 Max HP");
        AutoTestAssert.Equal(4, pcs.Energy, "GremlinKnob granted +1 Energy on Turn 1 (3 -> 4)");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "GremlinKnob drew +1 card on Turn 1 (5 -> 6)");
        var knob = AutoTestAssert.NotNull(player.Relics.OfType<GremlinKnob>().FirstOrDefault(), "Starter GremlinKnob");
        AutoTestAssert.Equal(RelicStatus.Disabled, knob.Status, "GremlinKnob disabled after Turn 1 trigger");
        AutoTestAssert.Equal(1, player.Creature.Powers.OfType<GremlinActivePower>().Count(), "Exactly 1 GremlinActivePower active");
        AutoTestAssert.Equal(5, GremlinMob.GetLivingCount(combatState, player), "All 5 Gremlins living");

        async Task ResetHandAndEnergy(int energy = 10)
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy > energy)
                await PlayerCmd.LoseEnergy(pcs.Energy - energy, player);
            else if (pcs.Energy < energy)
                await PlayerCmd.GainEnergy(energy - pcs.Energy, player);
            if (player.Creature.Block > 0)
                await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        }

        async Task SwapViaTagTeamAsync(GremlinKind targetKind)
        {
            if (GremlinMob.GetActiveKind(combatState, player) == targetKind)
                return;
            var tag = combatState.CreateCard<TagTeam>(player);
            await CardPileCmd.Add(tag, PileType.Hand);
            using (DownfallAutoTestRunner.PreferCards(c => c is GremlinChoiceCard gc && gc.Kind == targetKind))
            {
                AutoTestAssert.True(tag.TryManualPlay(null), $"Play TagTeam -> {targetKind}");
                await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            }
            AutoTestAssert.Equal(targetKind, GremlinMob.GetActiveKind(combatState, player), $"Swapped active Gremlin to {targetKind}");
            var sprite = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals?.Body as Sprite2D;
            AutoTestAssert.Equal(GremlinMob.SpriteTexturePathFor(targetKind), sprite?.Texture?.ResourcePath, $"Combat sprite updated to {targetKind}");
        }

        // 2. Mad Gremlin: attacked -> +2 FlexPotionPower (temp Strength), GremlinDance is AOE (AllEnemies)!
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Mad);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<MadGremlinPower>(), "MadGremlinPower(2) active");
        await CreatureCmd.GainBlock(player.Creature, 20m, ValueProp.Unpowered, null);
        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Move, dealer: enemy1, cardSource: null, cardPlay: null);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "MadGremlinPower granted +2 Strength when attacked");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<FlexPotionPower>(), "MadGremlinPower granted FlexPotionPower(2)");
        if (player.Creature.GetPower<FlexPotionPower>() is { } fp1) await PowerCmd.Remove(fp1);
        if (player.Creature.GetPower<StrengthPower>() is { } sp1) await PowerCmd.Remove(sp1);

        await ResetHandAndEnergy();
        var madDance = combatState.CreateCard<GremlinDance>(player);
        AutoTestAssert.Equal(TargetType.AllEnemies, madDance.TargetType, "Mad GremlinDance switches TargetType to AllEnemies");
        await CardPileCmd.Add(madDance, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(madDance.TryManualPlay(null), "Play Mad GremlinDance AOE");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "Mad GremlinDance dealt 6 to enemy1");
        AutoTestAssert.Equal(e2Hp - 6, enemy2.CurrentHp, "Mad GremlinDance dealt 6 to enemy2");

        // 3. Fat Gremlin: Attack applies 1 Weak even against blocked enemy; Fat GremlinDance applies 2 PiercingWailPower!
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Fat);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FatGremlinPower>(), "FatGremlinPower(1) active");
        await CreatureCmd.GainBlock(enemy1, 20m, ValueProp.Unpowered, null);
        var fatStrike = combatState.CreateCard<GremlinStrike>(player);
        await CardPileCmd.Add(fatStrike, PileType.Hand);
        AutoTestAssert.True(fatStrike.TryManualPlay(enemy1), "Play Strike as Fat Gremlin against blocked enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "FatGremlinPower applied 1 Weak even through enemy Block");
        await CreatureCmd.LoseBlock(ctx, enemy1, enemy1.Block, null);
        if (enemy1.GetPower<WeakPower>() is { } wp1) await PowerCmd.Remove(wp1);

        var fatDance = combatState.CreateCard<GremlinDance>(player);
        await CardPileCmd.Add(fatDance, PileType.Hand);
        AutoTestAssert.True(fatDance.TryManualPlay(enemy1), "Play Fat GremlinDance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(-2, enemy1.GetPowerAmount<StrengthPower>(), "Fat GremlinDance reduced enemy1 Strength by 2");
        AutoTestAssert.Equal(2, enemy1.GetPowerAmount<PiercingWailPower>(), "Fat GremlinDance applied PiercingWailPower(2)");
        if (enemy1.GetPower<PiercingWailPower>() is { } pwp1) await PowerCmd.Remove(pwp1);
        if (enemy1.GetPower<StrengthPower>() is { } esp1) await PowerCmd.Remove(esp1);
        if (enemy1.GetPower<WeakPower>() is { } wp2) await PowerCmd.Remove(wp2);

        // 4. Shield Gremlin: Skill grants +2 unpowered Block; Shield GremlinDance grants 6 Block + 6 damage!
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Shield);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<ShieldGremlinPower>(), "ShieldGremlinPower(2) active");
        var defend = combatState.CreateCard<GremlinDefend>(player);
        await CardPileCmd.Add(defend, PileType.Hand);
        AutoTestAssert.True(defend.TryManualPlay(null), "Play GremlinDefend as Shield Gremlin");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "GremlinDefend (5) + ShieldGremlinPower (2) = 7 Block");

        await ResetHandAndEnergy();
        var shieldDance = combatState.CreateCard<GremlinDance>(player);
        await CardPileCmd.Add(shieldDance, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(shieldDance.TryManualPlay(enemy1), "Play Shield GremlinDance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.Block, "Shield GremlinDance gained 6 Block");
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "Shield GremlinDance dealt 6 damage");

        // 5. Sneaky Gremlin: 0-cost Attack (Shiv) deals 4+2=6 + 2 random = 8 total; Sneaky GremlinDance draws 2!
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Sneaky);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<SneakyGremlinPower>(), "SneakyGremlinPower(2) active");
        var shiv = combatState.CreateCard<StsCards.Shiv>(player);
        await CardPileCmd.Add(shiv, PileType.Hand);
        int totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(shiv.TryManualPlay(enemy1), "Play 0-cost Shiv as Sneaky Gremlin");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 8, enemy1.CurrentHp + enemy2.CurrentHp, "Sneaky Gremlin boosted Shiv 4->6 + 2 random = 8 total damage");

        await ResetHandAndEnergy();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<GremlinDefend>(player), PileType.Draw, CardPilePosition.Top);
        var sneakyDance = combatState.CreateCard<GremlinDance>(player);
        await CardPileCmd.Add(sneakyDance, PileType.Hand);
        AutoTestAssert.True(sneakyDance.TryManualPlay(enemy1), "Play Sneaky GremlinDance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Sneaky GremlinDance drew 2 cards");

        // 6. Mid-play Swap non-double-trigger (Shield <-> Wizard):
        //    Playing TagTeam AS Shield -> Wizard grants +2 Block and 0 Wiz;
        //    Playing TagTeam AS Wizard -> Shield grants +1 Wiz and 0 Block!
        await ResetHandAndEnergy();
        if (player.Creature.GetPower<WizPower>() is { } preWiz) await PowerCmd.Remove(preWiz);
        await SwapViaTagTeamAsync(GremlinKind.Shield);
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Wizard);
        AutoTestAssert.Equal(2, player.Creature.Block, "TagTeam played as Shield->Wizard triggered departing Shield (+2 Block)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "TagTeam played as Shield->Wizard did NOT trigger arriving Wizard");

        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Shield);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WizPower>(), "TagTeam played as Wizard->Shield triggered departing Wizard (+1 Wiz)");
        AutoTestAssert.Equal(0, player.Creature.Block, "TagTeam played as Wizard->Shield did NOT trigger arriving Shield");
        if (player.Creature.GetPower<WizPower>() is { } preWiz2) await PowerCmd.Remove(preWiz2);

        // 7. Gremlin Wizard + Ward + WizPower (BOTH at 1 Wiz AND at 3 Wiz!):
        //    Play Ward (0-cost Skill -> 3 Block, Exhausts, +1 Wiz);
        //    Play Wizard GremlinDance at 1 Wiz -> deals unboosted 6 damage AND grants +2 Wiz = 3 Wiz WITHOUT consuming it!
        //    Play Wizard GremlinDance AT 3 Wiz -> deals 6 + 7 = 13 damage, consumes the pre-play 3 Wiz, AND KEEPS the 2 newly gained Wiz!
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Wizard);
        await ResetHandAndEnergy();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<GremlinWizardPower>(), "GremlinWizardPower(1) active");
        var ward = combatState.CreateCard<Ward>(player);
        await CardPileCmd.Add(ward, PileType.Hand);
        AutoTestAssert.True(ward.TryManualPlay(null), "Play Ward as Gremlin Wizard");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.Block, "Ward gained 3 Block");
        AutoTestAssert.Equal(PileType.Exhaust, ward.Pile?.Type, "Ward exhausted");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WizPower>(), "GremlinWizardPower granted 1 Wiz on Ward Skill");

        var wizDance1 = combatState.CreateCard<GremlinDance>(player);
        await CardPileCmd.Add(wizDance1, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(wizDance1.TryManualPlay(enemy1), "Play Wizard GremlinDance at 1 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "Wizard GremlinDance at 1 Wiz dealt unboosted 6 damage");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<WizPower>(), "Wizard GremlinDance reached 3 Wiz WITHOUT consuming its own Wiz");

        var wizDance2 = combatState.CreateCard<GremlinDance>(player);
        await CardPileCmd.Add(wizDance2, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(wizDance2.TryManualPlay(enemy1), "Play Wizard GremlinDance AT 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 13, enemy1.CurrentHp, "Wizard GremlinDance at 3 Wiz dealt boosted 6 + 7 = 13 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<WizPower>(), "Wizard GremlinDance consumed pre-play 3 Wiz and RETAINED newly gained 2 Wiz");

        await PowerCmd.Apply<WizPower>(ctx, player.Creature, 1m, player.Creature, null); // back to 3 Wiz
        var wizStrike = combatState.CreateCard<GremlinStrike>(player);
        await CardPileCmd.Add(wizStrike, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(wizStrike.TryManualPlay(enemy1), "Play Strike at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 13, enemy1.CurrentHp, "3 Wiz boosted Strike from 6 -> 13 damage (+7)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "3 Wiz fully consumed after normal Attack");

        // 8. Upgrades: GremlinDance+ (9 dmg / 9 Block), TagTeam+ (Retain), Ward+ (5 Block)!
        var upgDance = combatState.CreateCard<GremlinDance>(player);
        CardCmd.Upgrade(upgDance);
        AutoTestAssert.Equal(9m, upgDance.DynamicVars.Damage.BaseValue, "GremlinDance+ has 9 Damage");
        AutoTestAssert.Equal(9m, upgDance.DynamicVars.Block.BaseValue, "GremlinDance+ has 9 Block");
        var upgTag = combatState.CreateCard<TagTeam>(player);
        CardCmd.Upgrade(upgTag);
        AutoTestAssert.True(upgTag.Keywords.Contains(CardKeyword.Retain), "TagTeam+ has Retain");
        var upgWard = combatState.CreateCard<Ward>(player);
        CardCmd.Upgrade(upgWard);
        AutoTestAssert.Equal(5m, upgWard.DynamicVars.Block.BaseValue, "Ward+ has 5 Block");

        // 9. DownfallTempHpPower: stacking (5+5=10), Block absorbs Move damage before TempHP, partial & overflow!
        await ResetHandAndEnergy();
        await PowerCmd.Apply<DownfallTempHpPower>(ctx, player.Creature, 5m, player.Creature, null);
        await PowerCmd.Apply<DownfallTempHpPower>(ctx, player.Creature, 5m, player.Creature, null);
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Temp HP stacked 5 + 5 = 10");
        await CreatureCmd.GainBlock(player.Creature, 8m, ValueProp.Unpowered, null);
        int playerHp = player.Creature.CurrentHp;
        await CreatureCmd.Damage(ctx, player.Creature, 6m, ValueProp.Move, enemy1, null, null);
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Block absorbed 6 Move damage before Temp HP was touched");
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await CreatureCmd.Damage(ctx, player.Creature, 6m, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.Equal(playerHp, player.Creature.CurrentHp, "Temp HP absorbed full 6 damage (0 real HP lost)");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Temp HP decremented 10 -> 4");
        await CreatureCmd.Damage(ctx, player.Creature, 9m, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.Equal(playerHp - 5, player.Creature.CurrentHp, "Temp HP absorbed 4 of 9 damage (5 real HP lost)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Temp HP depleted and removed");

        // 10. Echo helper: Attack gets Ethereal + Exhaust; Power gets Ethereal WITHOUT Exhaust; Echoes cannot be Echoed!
        var origStrike = combatState.CreateCard<GremlinStrike>(player);
        var echo = AutoTestAssert.NotNull(GremlinMob.MakeEcho(origStrike), "MakeEcho created Attack clone");
        AutoTestAssert.True(echo.Keywords.Contains(CardKeyword.Ethereal), "Attack Echo has Ethereal");
        AutoTestAssert.True(echo.Keywords.Contains(CardKeyword.Exhaust), "Attack Echo has Exhaust");
        AutoTestAssert.True(GremlinMob.MakeEcho(echo) is null, "Echo cannot be Echoed again");

        var samplePower = combatState.CreateCard<StsCards.Inflame>(player);
        var powerEcho = AutoTestAssert.NotNull(GremlinMob.MakeEcho(samplePower), "MakeEcho created Power clone");
        AutoTestAssert.True(powerEcho.Keywords.Contains(CardKeyword.Ethereal), "Power Echo has Ethereal");
        AutoTestAssert.True(!powerEcho.Keywords.Contains(CardKeyword.Exhaust), "Power Echo does NOT have Exhaust");

        // 11. Nob mode (EnterNobAsync / RevertNobAsync): blocks Swap, removes active Gremlin power, GremlinDance grants +2 Strength, Revert restores!
        await ResetHandAndEnergy();
        GremlinKind preNobKind = GremlinMob.GetActiveKind(combatState, player);
        await GremlinMob.EnterNobAsync(player, combatState, ctx);
        AutoTestAssert.True(GremlinMob.IsNob(combatState, player), "EnterNobAsync set IsNob");
        AutoTestAssert.Equal(0, player.Creature.Powers.OfType<GremlinActivePower>().Count(), "EnterNobAsync removed active Gremlin power");
        AutoTestAssert.True(!await GremlinMob.SwapNextAsync(player, combatState, ctx), "SwapNextAsync blocked while in Nob form");
        var nobDance = combatState.CreateCard<GremlinDance>(player);
        await CardPileCmd.Add(nobDance, PileType.Hand);
        AutoTestAssert.True(nobDance.TryManualPlay(enemy1), "Play Nob GremlinDance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Nob GremlinDance granted +2 permanent Strength");
        await GremlinMob.RevertNobAsync(player, combatState, ctx);
        AutoTestAssert.True(!GremlinMob.IsNob(combatState, player), "RevertNobAsync cleared IsNob");
        AutoTestAssert.Equal(preNobKind, GremlinMob.GetActiveKind(combatState, player), "RevertNobAsync preserved active Gremlin");
        AutoTestAssert.Equal(1, player.Creature.Powers.OfType<GremlinActivePower>().Count(), "RevertNobAsync restored active Gremlin power");
        if (player.Creature.GetPower<StrengthPower>() is { } nobStr) await PowerCmd.Remove(nobStr);

        // 12. Multi-turn lifecycle:
        //     - Swap to Mad Gremlin, give 50 Block so player takes 0 HP loss while enemy attacks trigger MadGremlinPower!
        //     - Advance Turn 1 -> Turn 2: GremlinKnob does NOT trigger again (Energy == 3), MadGremlinPower FlexPotionPower persists into Turn 2!
        //     - Advance Turn 2 -> Turn 3 (suppress enemy attacks): FlexPotionPower expires at end of Player Turn 2 (Strength == 0 on Turn 3)!
        await ResetHandAndEnergy();
        await SwapViaTagTeamAsync(GremlinKind.Mad);
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(player.MaxEnergy, pcs.Energy, "GremlinKnob did NOT grant extra Energy on Turn 2");
        AutoTestAssert.True(player.Creature.GetPowerAmount<StrengthPower>() >= 2, "MadGremlinPower Strength gained on Enemy Turn 1 persisted into Player Turn 2");
        AutoTestAssert.True(player.Creature.GetPowerAmount<FlexPotionPower>() >= 2, "MadGremlinPower FlexPotionPower persisted into Player Turn 2");

        // Suppress enemy attacks for Turn 2->3 by swapping off Mad -> Fat so enemy doesn't re-trigger MadGremlinPower!
        await SwapViaTagTeamAsync(GremlinKind.Fat);
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<FlexPotionPower>(), "FlexPotionPower expired at end of Player Turn 2");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "Temporary Strength reverted to 0 by Turn 3");
    }
}
