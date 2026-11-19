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

    public static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGremlins>("DFGRM02");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

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

        async Task CleanPlayerBuffs()
        {
            if (player.Creature.GetPower<WizPower>() is { } w) await PowerCmd.Remove(w);
            if (player.Creature.GetPower<FlexPotionPower>() is { } f) await PowerCmd.Remove(f);
            if (player.Creature.GetPower<StrengthPower>() is { } s) await PowerCmd.Remove(s);
            if (player.Creature.GetPower<DownfallTempHpPower>() is { } t) await PowerCmd.Remove(t);
        }

        // 1. BulkUp: +4 Temp HP + Swap to Fat Gremlin!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var bulkUp = combatState.CreateCard<BulkUp>(player);
        await CardPileCmd.Add(bulkUp, PileType.Hand);
        AutoTestAssert.True(bulkUp.TryManualPlay(null), "Play BulkUp");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "BulkUp granted 4 Temp HP");
        AutoTestAssert.Equal(GremlinKind.Fat, GremlinMob.GetActiveKind(combatState, player), "BulkUp swapped to Fat Gremlin");

        // 2. CatScratch (multi-hit Attack as Fat Gremlin: deals 2x3=6 AND FatGremlinPower only applies 1 Weak, NOT 3!).
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var catScratch = combatState.CreateCard<CatScratch>(player);
        await CardPileCmd.Add(catScratch, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(catScratch.TryManualPlay(enemy1), "Play CatScratch as Fat Gremlin");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "CatScratch dealt 2x3 = 6 damage");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "FatGremlinPower applied exactly 1 Weak across 3 hits of CatScratch");

        // 3. ToeStub: Fat Gremlin + ToeStub on clean enemy2 -> Fat applies 1 Weak, then ToeStub applies 2 Vulnerable!
        await ResetHandAndEnergy();
        var toeFatClean = combatState.CreateCard<ToeStub>(player);
        await CardPileCmd.Add(toeFatClean, PileType.Hand);
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(toeFatClean.TryManualPlay(enemy2), "Play ToeStub as Fat Gremlin on clean enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 5, enemy2.CurrentHp, "ToeStub dealt 5 damage");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<WeakPower>(), "FatGremlinPower applied 1 Weak to enemy2");
        AutoTestAssert.Equal(2, enemy2.GetPowerAmount<VulnerablePower>(), "ToeStub applied 2 Vulnerable after Fat weakened enemy2");
        if (enemy2.GetPower<VulnerablePower>() is { } v2) await PowerCmd.Remove(v2);
        if (enemy2.GetPower<WeakPower>() is { } w2) await PowerCmd.Remove(w2);

        // 4. PourSalt: Fat + PourSalt on clean enemy2 -> Fat adds 1 Weak, PourSalt consumes it (net 0 Weak) and adds 2 Shivs!
        await ResetHandAndEnergy();
        var pourSaltClean = combatState.CreateCard<PourSalt>(player);
        await CardPileCmd.Add(pourSaltClean, PileType.Hand);
        AutoTestAssert.True(pourSaltClean.TryManualPlay(enemy2), "Play PourSalt as Fat Gremlin on clean enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, enemy2.GetPowerAmount<WeakPower>(), "Fat applied 1 Weak and PourSalt consumed it (net 0 Weak)");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<StsCards.Shiv>().Count(), "PourSalt added 2 Shivs after Fat weakened enemy2");

        // 5. GlitterGuard+ adds 2 Upgraded Wards and Swaps to Shield Gremlin!
        await ResetHandAndEnergy();
        var glitterPlus = combatState.CreateCard<GlitterGuard>(player);
        CardCmd.Upgrade(glitterPlus);
        await CardPileCmd.Add(glitterPlus, PileType.Hand);
        AutoTestAssert.True(glitterPlus.TryManualPlay(null), "Play GlitterGuard+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<Ward>().Count(w => w.IsUpgraded), "GlitterGuard+ added 2 Upgraded Wards");
        AutoTestAssert.Equal(GremlinKind.Shield, GremlinMob.GetActiveKind(combatState, player), "GlitterGuard+ swapped to Shield Gremlin");

        // Negative tests as non-Fat (Shield) on clean enemy2: ToeStub -> 0 Vuln, PourSalt -> 0 Shivs!
        await ResetHandAndEnergy();
        var toeNoWeak = combatState.CreateCard<ToeStub>(player);
        await CardPileCmd.Add(toeNoWeak, PileType.Hand);
        AutoTestAssert.True(toeNoWeak.TryManualPlay(enemy2), "Play ToeStub as Shield on non-Weak enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, enemy2.GetPowerAmount<VulnerablePower>(), "ToeStub applied 0 Vulnerable on non-Weak enemy2");

        var pourNoWeak = combatState.CreateCard<PourSalt>(player);
        await CardPileCmd.Add(pourNoWeak, PileType.Hand);
        AutoTestAssert.True(pourNoWeak.TryManualPlay(enemy2), "Play PourSalt as Shield on non-Weak enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, pcs.Hand.Cards.OfType<StsCards.Shiv>().Count(), "PourSalt added 0 Shivs on non-Weak enemy2");

        // Lethal-kill fallback: killing a Weak enemy with PourSalt STILL grants 2 Shivs; killing a debuffed enemy with Jeer STILL grants 2 Temp HP!
        var frailKill1 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.Damage(ctx, frailKill1, frailKill1.CurrentHp - 1, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await PowerCmd.Apply<WeakPower>(ctx, frailKill1, 1m, player.Creature, null);
        var pourLethal = combatState.CreateCard<PourSalt>(player);
        await CardPileCmd.Add(pourLethal, PileType.Hand);
        AutoTestAssert.True(pourLethal.TryManualPlay(frailKill1), "Play PourSalt to kill Weak enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(frailKill1.IsDead, "PourSalt killed Weak enemy");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<StsCards.Shiv>().Count(), "PourSalt still granted 2 Shivs on lethal kill of Weak enemy");

        var frailKill2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.Damage(ctx, frailKill2, frailKill2.CurrentHp - 1, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await PowerCmd.Apply<WeakPower>(ctx, frailKill2, 1m, player.Creature, null);
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var jeerLethal = combatState.CreateCard<Jeer>(player);
        await CardPileCmd.Add(jeerLethal, PileType.Hand);
        AutoTestAssert.True(jeerLethal.TryManualPlay(frailKill2), "Play Jeer to kill debuffed enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(frailKill2.IsDead, "Jeer killed debuffed enemy");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Jeer still granted 2 Temp HP on lethal kill of debuffed enemy");

        // 6. Jeer: enemy2 has NO debuff -> 0 Temp HP; enemy1 HAS Weak -> +2 Temp HP!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var jeerNoDebuff = combatState.CreateCard<Jeer>(player);
        await CardPileCmd.Add(jeerNoDebuff, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(jeerNoDebuff.TryManualPlay(enemy2), "Play Jeer on non-debuffed enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 7, enemy2.CurrentHp, "Jeer dealt 7 damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Jeer granted 0 Temp HP against clean enemy2");

        var jeerDebuffed = combatState.CreateCard<Jeer>(player);
        await CardPileCmd.Add(jeerDebuffed, PileType.Hand);
        AutoTestAssert.True(jeerDebuffed.TryManualPlay(enemy1), "Play Jeer on Weak enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Jeer granted 2 Temp HP against debuffed enemy1");
        await CleanPlayerBuffs();

        // 7. Irritability: 6 Block + 3 temporary Thorns (ChoBarbsPower) + Swap to Mad Gremlin!
        //    Pretaliation: deals 6x2=12, enemy1 hits back for 3 (triggering Mad +2 temp Str AND Thorns 3 dmg back);
        //    Pretaliation lethal kill skips retaliation! Pretaliation+ upgrades to 8 dmg / 1 Retaliate!
        await ResetHandAndEnergy();
        var irrit = combatState.CreateCard<Irritability>(player);
        await CardPileCmd.Add(irrit, PileType.Hand);
        AutoTestAssert.True(irrit.TryManualPlay(null), "Play Irritability (departing Shield adds +2 Block -> 8 total)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "Irritability (6) + departing Shield (2) = 8 Block");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ThornsPower>(), "Irritability granted 3 Thorns");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ChoBarbsPower>(), "Irritability applied ChoBarbsPower(3)");
        AutoTestAssert.Equal(GremlinKind.Mad, GremlinMob.GetActiveKind(combatState, player), "Irritability swapped to Mad Gremlin");

        var pretal = combatState.CreateCard<Pretaliation>(player);
        await CardPileCmd.Add(pretal, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(pretal.TryManualPlay(enemy1), "Play Pretaliation as Mad Gremlin with Thorns");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 15, enemy1.CurrentHp, "Pretaliation dealt 6x2 = 12 + 3 Thorns retaliation = 15 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Pretaliation enemy retaliation triggered MadGremlinPower (+2 temp Strength)");
        await CleanPlayerBuffs();

        var frailKill3 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.Damage(ctx, frailKill3, frailKill3.CurrentHp - 1, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await ResetHandAndEnergy();
        var pretalLethal = combatState.CreateCard<Pretaliation>(player);
        CardCmd.Upgrade(pretalLethal);
        AutoTestAssert.Equal(8m, pretalLethal.DynamicVars.Damage.BaseValue, "Pretaliation+ Damage == 8");
        AutoTestAssert.Equal(1m, pretalLethal.DynamicVars["Retaliate"].BaseValue, "Pretaliation+ Retaliate == 1");
        await CardPileCmd.Add(pretalLethal, PileType.Hand);
        int playerHpBeforeLethalPretal = player.Creature.CurrentHp;
        AutoTestAssert.True(pretalLethal.TryManualPlay(frailKill3), "Play Pretaliation+ for lethal kill");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(frailKill3.IsDead, "Pretaliation+ killed frailKill3");
        AutoTestAssert.Equal(playerHpBeforeLethalPretal, player.Creature.CurrentHp, "Lethal Pretaliation skipped dead enemy retaliation");

        // 8. GremlinMeal (+2 temp Str) + FollowThrough (8 dmg + 1 temp Str):
        await ResetHandAndEnergy();
        var meal = combatState.CreateCard<GremlinMeal>(player);
        await CardPileCmd.Add(meal, PileType.Hand);
        AutoTestAssert.True(meal.TryManualPlay(null), "Play GremlinMeal");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "GremlinMeal granted +2 temp Strength");

        var ft = combatState.CreateCard<FollowThrough>(player);
        await CardPileCmd.Add(ft, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(ft.TryManualPlay(enemy2), "Play FollowThrough with +2 Strength");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 10, enemy2.CurrentHp, "FollowThrough dealt 8 + 2 Str = 10 damage");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "FollowThrough granted +1 temp Strength (2 -> 3)");
        await CleanPlayerBuffs();

        // 9. Tadah! (1 Ward + Swap to Wizard) + Changeo (8 Block + 1 Wiz + 1 from Wizard) + Presto (9 dmg + 1 Wiz) + Glimmer (2 dmg + 2 Wards)!
        await ResetHandAndEnergy();
        var tadah = combatState.CreateCard<Tadah>(player);
        await CardPileCmd.Add(tadah, PileType.Hand);
        AutoTestAssert.True(tadah.TryManualPlay(null), "Play Tadah!");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<Ward>().Count(), "Tadah! added 1 Ward to Hand");
        AutoTestAssert.Equal(GremlinKind.Wizard, GremlinMob.GetActiveKind(combatState, player), "Tadah! swapped to Gremlin Wizard");

        var changeo = combatState.CreateCard<Changeo>(player);
        await CardPileCmd.Add(changeo, PileType.Hand);
        AutoTestAssert.True(changeo.TryManualPlay(null), "Play Changeo as Gremlin Wizard");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "Changeo gained 8 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<WizPower>(), "Changeo (1) + Wizard passive (1) = 2 Wiz");

        var presto = combatState.CreateCard<Presto>(player);
        await CardPileCmd.Add(presto, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(presto.TryManualPlay(enemy2), "Play Presto at 2 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 9, enemy2.CurrentHp, "Presto at 2 Wiz dealt unboosted 9 damage");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<WizPower>(), "Presto granted +1 Wiz -> 3 Wiz without consuming it");

        var glimmer = combatState.CreateCard<Glimmer>(player);
        await CardPileCmd.Add(glimmer, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(glimmer.TryManualPlay(enemy2), "Play Glimmer at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 9, enemy2.CurrentHp, "Glimmer at 3 Wiz dealt 2 + 7 = 9 damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "Glimmer consumed 3 Wiz");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.OfType<Ward>().Count(), "Glimmer added 2 Wards (1 + 2 = 3)");

        // 10. GremlinArms+: triggers active Wizard bonus TWICE (+2 Wiz + 1 from Wizard Skill passive = 3 Wiz), then Swaps to chosen Gremlin (Fat)!
        await ResetHandAndEnergy();
        var armsPlus = combatState.CreateCard<GremlinArms>(player);
        CardCmd.Upgrade(armsPlus);
        await CardPileCmd.Add(armsPlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is GremlinChoiceCard { Kind: GremlinKind.Fat }))
        {
            AutoTestAssert.True(armsPlus.TryManualPlay(null), "Play GremlinArms+ as Wizard -> Fat");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<WizPower>(), "GremlinArms+ triggered Wizard passive (+1) + Wizard bonus x2 (+2) = 3 Wiz");
        AutoTestAssert.Equal(GremlinKind.Fat, GremlinMob.GetActiveKind(combatState, player), "GremlinArms+ swapped to Fat");
        await CleanPlayerBuffs();

        // 11. Patsy (4 Block + SwapNext):
        await ResetHandAndEnergy();
        GremlinKind beforePatsy = GremlinMob.GetActiveKind(combatState, player);
        var patsy = combatState.CreateCard<Patsy>(player);
        await CardPileCmd.Add(patsy, PileType.Hand);
        AutoTestAssert.True(patsy.TryManualPlay(null), "Play Patsy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "Patsy gained 4 Block");
        AutoTestAssert.True(GremlinMob.GetActiveKind(combatState, player) != beforePatsy, "Patsy swapped to next Gremlin");

        // 12. Tricksy: keeps pre-existing Hand Skill, draws 4 (2 Attacks + 2 Skills), discards ONLY newly drawn Skills, Swaps to Sneaky!
        await ResetHandAndEnergy();
        var preExistingSkill = combatState.CreateCard<GremlinDefend>(player);
        await CardPileCmd.Add(preExistingSkill, PileType.Hand);
        var tkSkill1 = combatState.CreateCard<GremlinDefend>(player);
        var tkAtk1 = combatState.CreateCard<GremlinStrike>(player);
        var tkSkill2 = combatState.CreateCard<GremlinDefend>(player);
        var tkAtk2 = combatState.CreateCard<GremlinStrike>(player);
        await CardPileCmd.Add(tkSkill1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(tkAtk1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(tkSkill2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(tkAtk2, PileType.Draw, CardPilePosition.Top);
        var tricksy = combatState.CreateCard<Tricksy>(player);
        await CardPileCmd.Add(tricksy, PileType.Hand);
        AutoTestAssert.True(tricksy.TryManualPlay(null), "Play Tricksy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "Tricksy kept pre-existing Skill + 2 drawn Attacks and discarded 2 drawn Skills");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(preExistingSkill), "Tricksy did NOT discard pre-existing Hand Skill");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(tkAtk1) && pcs.Hand.Cards.Contains(tkAtk2), "Tricksy kept both drawn Attacks");
        AutoTestAssert.Equal(PileType.Discard, tkSkill1.Pile?.Type, "Tricksy discarded non-Attack Skill 1");
        AutoTestAssert.Equal(PileType.Discard, tkSkill2.Pile?.Type, "Tricksy discarded non-Attack Skill 2");
        AutoTestAssert.Equal(GremlinKind.Sneaky, GremlinMob.GetActiveKind(combatState, player), "Tricksy swapped to Sneaky Gremlin");

        // 13. TwistTheKnife+ (7 dmg + Upgraded Shiv) + ShankStone (Unplayable, draws -> adds 2 Shivs) + Pinprick+ (draws -> Echoes itself; drawing Echo does NOT re-Echo)!
        await ResetHandAndEnergy();
        var ttkPlus = combatState.CreateCard<TwistTheKnife>(player);
        CardCmd.Upgrade(ttkPlus);
        await CardPileCmd.Add(ttkPlus, PileType.Hand);
        int totalBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(ttkPlus.TryManualPlay(enemy2), "Play TwistTheKnife+ as Sneaky Gremlin");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalBefore - 9, enemy1.CurrentHp + enemy2.CurrentHp, "TwistTheKnife+ (7) + Sneaky random (2) = 9 damage");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<StsCards.Shiv>().Count(s => s.IsUpgraded), "TwistTheKnife+ added 1 Upgraded Shiv");

        await ResetHandAndEnergy();
        var shank = combatState.CreateCard<ShankStone>(player);
        await CardPileCmd.Add(shank, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1m, player);
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<StsCards.Shiv>().Count(), "Drawing ShankStone added 2 Shivs to Hand");
        AutoTestAssert.True(!shank.CanPlay(), "ShankStone is Unplayable");

        await ResetHandAndEnergy();
        var pinPlus = combatState.CreateCard<Pinprick>(player);
        CardCmd.Upgrade(pinPlus);
        await CardPileCmd.Add(combatState.CreateCard<GremlinDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(pinPlus, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1m, player);
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<Pinprick>().Count(), "Drawing Pinprick+ created an Echo of itself in Hand");
        var pinEcho = pcs.Hand.Cards.OfType<Pinprick>().First(c => GremlinMob.IsEcho(c));
        AutoTestAssert.True(pinEcho.Keywords.Contains(CardKeyword.Ethereal), "Pinprick+ Echo has Ethereal");

        // Drawing the Echo itself must NOT re-Echo!
        await CardPileCmd.Add(pinEcho, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1m, player);
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<Pinprick>().Count(), "Drawing Pinprick+ Echo did NOT re-Echo itself");

        totalBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(pinEcho.TryManualPlay(enemy2), "Play Pinprick+ Echo as Sneaky Gremlin");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalBefore - 5, enemy1.CurrentHp + enemy2.CurrentHp, "0-cost Pinprick (1+2 Sneaky) + 2 Sneaky random = 5 total damage");
        AutoTestAssert.Equal(PileType.Exhaust, pinEcho.Pile?.Type, "Pinprick Echo exhausted");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Pinprick Echo drew 1 card (original Pinprick+ + drawn Defend = 2)");

        // 14. Irritability temporary Thorns (ChoBarbsPower) multi-turn lifecycle:
        //     Persists through Enemy Turn 1 (dealing 3 Thorns damage to attacking enemies!) and expires to 0 at Round 2 start!
        await ResetHandAndEnergy();
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ThornsPower>(), "Irritability 3 Thorns still active before ending Turn 1");
        int totalEnemyHpBeforeEnemyTurn = enemy1.CurrentHp + enemy2.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.True(enemy1.CurrentHp + enemy2.CurrentHp <= totalEnemyHpBeforeEnemyTurn - 3,
            "Irritability Thorns persisted through Enemy Turn 1 and damaged attacking enemy");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ThornsPower>(), "Irritability temporary Thorns expired to 0 by Round 2 start");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ChoBarbsPower>(), "ChoBarbsPower removed by Round 2 start");
    }

    public static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGremlins>("DFGRM03");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var cultist = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.CalcifiedCultist>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 1200m);
        await CreatureCmd.GainMaxHp(enemy2, 1200m);
        await CreatureCmd.GainMaxHp(cultist, 1200m);

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

        async Task CleanPlayerBuffs()
        {
            if (player.Creature.GetPower<WizPower>() is { } w) await PowerCmd.Remove(w);
            if (player.Creature.GetPower<StrengthPower>() is { } s) await PowerCmd.Remove(s);
            if (player.Creature.GetPower<DownfallTempHpPower>() is { } t) await PowerCmd.Remove(t);
            if (player.Creature.GetPower<BubbleBarrierPower>() is { } b) await PowerCmd.Remove(b);
            if (player.Creature.GetPower<EnthusiasmPower>() is { } e) await PowerCmd.Remove(e);
        }

        // Start cleanly as Shield Gremlin so Fat/Sneaky don't perturb exact numbers unless intended.
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Shield, ctx);

        // 1. AggressiveDefense (BashedPower) + BubbleBarrier (BubbleBarrierPower):
        //    AggressiveDefense deals 8 + applies BashedPower(3) to enemy1.
        //    BubbleBarrier applies BubbleBarrierPower(2) (does NOT self-trigger!);
        //    Playing GremlinStrike gains 2 Block from BubbleBarrier -> triggers BashedPower (-3 HP on enemy1)!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var aggDef = combatState.CreateCard<AggressiveDefense>(player);
        await CardPileCmd.Add(aggDef, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(aggDef.TryManualPlay(enemy1), "Play AggressiveDefense");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 8, enemy1.CurrentHp, "AggressiveDefense dealt 8 damage");
        AutoTestAssert.Equal(3, enemy1.GetPowerAmount<BashedPower>(), "AggressiveDefense applied BashedPower(3)");

        await ResetHandAndEnergy();
        var bubble = combatState.CreateCard<BubbleBarrier>(player);
        await CardPileCmd.Add(bubble, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(bubble.TryManualPlay(null), "Play BubbleBarrier as Shield Gremlin (Shield +2 Block triggers Bashed once; BubbleBarrier itself does NOT self-trigger)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.Block, "BubbleBarrier did NOT self-trigger (only Shield's 2 Block)");
        AutoTestAssert.Equal(e1Hp - 3, enemy1.CurrentHp, "Shield's 2 Block triggered BashedPower (-3 HP)");

        var bbStrike = combatState.CreateCard<GremlinStrike>(player);
        await CardPileCmd.Add(bbStrike, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(bbStrike.TryManualPlay(enemy1), "Play Strike under BubbleBarrierPower(2) + BashedPower(3)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "BubbleBarrierPower gained +2 Block (2 -> 4)");
        AutoTestAssert.Equal(e1Hp - 9, enemy1.CurrentHp, "Strike dealt 6 + BubbleBarrier Block triggered BashedPower (-3) = 9 total");

        // 2. ArmsTheft: applies 1 Weak, steals 1 Strength; blocked by Artifact -> player gains 0 Strength!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var arms1 = combatState.CreateCard<ArmsTheft>(player);
        await CardPileCmd.Add(arms1, PileType.Hand);
        AutoTestAssert.True(arms1.TryManualPlay(enemy1), "Play ArmsTheft on enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "ArmsTheft applied 1 Weak");
        AutoTestAssert.Equal(-1, enemy1.GetPowerAmount<StrengthPower>(), "ArmsTheft reduced enemy1 Strength by 1");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "ArmsTheft stole +1 Strength");
        AutoTestAssert.Equal(PileType.Exhaust, arms1.Pile?.Type, "ArmsTheft exhausted");

        await CleanPlayerBuffs();
        await PowerCmd.Apply<ArtifactPower>(ctx, enemy2, 2m, enemy2, null);
        var armsArt = combatState.CreateCard<ArmsTheft>(player);
        await CardPileCmd.Add(armsArt, PileType.Hand);
        AutoTestAssert.True(armsArt.TryManualPlay(enemy2), "Play ArmsTheft on Artifact(2) enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, enemy2.GetPowerAmount<StrengthPower>(), "Artifact blocked ArmsTheft Strength loss");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "Player stole 0 Strength when Artifact blocked ArmsTheft");

        // 3. Astound: <3 Wiz -> 5 Block, 0 Wards; >=3 Wiz -> 5 Block + 2 Wards (Upgraded when +)!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx); // avoid Shield passive Block
        var astLow = combatState.CreateCard<Astound>(player);
        await CardPileCmd.Add(astLow, PileType.Hand);
        AutoTestAssert.True(astLow.TryManualPlay(null), "Play Astound at 0 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.Block, "Astound gained 5 Block");
        AutoTestAssert.Equal(0, pcs.Hand.Cards.OfType<Ward>().Count(), "Astound at <3 Wiz added 0 Wards");

        await ResetHandAndEnergy();
        await PowerCmd.Apply<WizPower>(ctx, player.Creature, 3m, player.Creature, null);
        var astHighPlus = combatState.CreateCard<Astound>(player);
        CardCmd.Upgrade(astHighPlus);
        await CardPileCmd.Add(astHighPlus, PileType.Hand);
        AutoTestAssert.True(astHighPlus.TryManualPlay(null), "Play Astound+ at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "Astound+ gained 7 Block");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<Ward>().Count(w => w.IsUpgraded), "Astound+ at >=3 Wiz added 2 Upgraded Wards");

        // 4. Kablamo! at 3 Wiz: deals (4+7)x2 = 22 to ALL enemies WITHOUT consuming Wiz!
        await ResetHandAndEnergy();
        var kablamo = combatState.CreateCard<Kablamo>(player);
        await CardPileCmd.Add(kablamo, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        int cHp = cultist.CurrentHp;
        AutoTestAssert.True(kablamo.TryManualPlay(null), "Play Kablamo! at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 22, enemy1.CurrentHp, "Kablamo! dealt (4+7)x2 = 22 to enemy1");
        AutoTestAssert.Equal(e2Hp - 22, enemy2.CurrentHp, "Kablamo! dealt (4+7)x2 = 22 to enemy2");
        AutoTestAssert.Equal(cHp - 22, cultist.CurrentHp, "Kablamo! dealt (4+7)x2 = 22 to cultist");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<WizPower>(), "Kablamo! did NOT consume Wiz");

        // 5. Bang at 3 Wiz: 0 base x 3 hits -> (0+7)x3 = 21 damage, consumes Wiz, Exhausts, puts Whiz in Discard!
        await ResetHandAndEnergy();
        var bang = combatState.CreateCard<Bang>(player);
        await CardPileCmd.Add(bang, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(bang.TryManualPlay(enemy2), "Play Bang at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 21, enemy2.CurrentHp, "Bang at 3 Wiz dealt (0+7)x3 = 21 damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "Bang consumed 3 Wiz");
        AutoTestAssert.Equal(PileType.Exhaust, bang.Pile?.Type, "Bang exhausted");
        AutoTestAssert.True(PileType.Discard.GetPile(player).Cards.OfType<Whiz>().Any(), "Bang added Whiz to Discard Pile");

        // 6. Dazzle: <3 Wiz -> 9 dmg, 0 Strength stolen; >=3 Wiz -> 9+7=16 dmg + steals 2 Strength, Exhausts!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var dazzleLow = combatState.CreateCard<Dazzle>(player);
        await CardPileCmd.Add(dazzleLow, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(dazzleLow.TryManualPlay(enemy2), "Play Dazzle at 0 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 9, enemy2.CurrentHp, "Dazzle at 0 Wiz dealt 9 damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "Dazzle at 0 Wiz stole 0 Strength");

        await PowerCmd.Apply<WizPower>(ctx, player.Creature, 3m, player.Creature, null);
        var dazzleHigh = combatState.CreateCard<Dazzle>(player);
        await CardPileCmd.Add(dazzleHigh, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(dazzleHigh.TryManualPlay(enemy2), "Play Dazzle at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 16, enemy2.CurrentHp, "Dazzle at 3 Wiz dealt 9 + 7 = 16 damage");
        AutoTestAssert.Equal(-2, enemy2.GetPowerAmount<StrengthPower>(), "Dazzle at 3 Wiz reduced enemy2 Strength by 2");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Dazzle at 3 Wiz stole +2 Strength");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "Dazzle consumed 3 Wiz");
        AutoTestAssert.Equal(PileType.Exhaust, dazzleHigh.Pile?.Type, "Dazzle exhausted");
        await CleanPlayerBuffs();

        // 7. BurlyBlow: 0 Weak -> 1 hit (4); 3 Weak -> 1 + 3 = 4 hits (16)!
        await ResetHandAndEnergy();
        var burly0 = combatState.CreateCard<BurlyBlow>(player);
        await CardPileCmd.Add(burly0, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(burly0.TryManualPlay(enemy2), "Play BurlyBlow on 0-Weak enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 4, enemy2.CurrentHp, "BurlyBlow on 0 Weak dealt 1x4 = 4 damage");

        await PowerCmd.Apply<WeakPower>(ctx, enemy2, 3m, player.Creature, null);
        var burly3 = combatState.CreateCard<BurlyBlow>(player);
        await CardPileCmd.Add(burly3, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(burly3.TryManualPlay(enemy2), "Play BurlyBlow on 3-Weak enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 16, enemy2.CurrentHp, "BurlyBlow on 3 Weak dealt 4x4 = 16 damage");
        if (enemy2.GetPower<WeakPower>() is { } wClean2) await PowerCmd.Remove(wClean2);

        // 8. CounterStrike (intendsToAttack vs non-attacking CalcifiedCultist!) + FeelTheAudience!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Wizard, ctx);
        AutoTestAssert.True(cultist.Monster is { IntendsToAttack: false }, "CalcifiedCultist starts non-attacking");
        var csNoAtk = combatState.CreateCard<CounterStrike>(player);
        AutoTestAssert.True(csNoAtk.Tags.Contains(CardTag.Strike), "CounterStrike carries CardTag.Strike");
        await CardPileCmd.Add(csNoAtk, PileType.Hand);
        AutoTestAssert.True(csNoAtk.TryManualPlay(cultist), "Play CounterStrike on non-attacking cultist");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "CounterStrike did NOT trigger bonus on non-attacking cultist");

        var attackingEnemy = combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive && e.Monster is { IntendsToAttack: true });
        AutoTestAssert.NotNull(attackingEnemy, "At least one Nibbit intends to attack on Turn 1");
        var csAtk = combatState.CreateCard<CounterStrike>(player);
        await CardPileCmd.Add(csAtk, PileType.Hand);
        AutoTestAssert.True(csAtk.TryManualPlay(attackingEnemy!), "Play CounterStrike on attacking enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<WizPower>(), "CounterStrike triggered Wizard bonus twice (+2 Wiz)");
        await CleanPlayerBuffs();

        int expectedAttackers = combatState.HittableEnemies.Count(e => e.IsAlive && e.Monster is { IntendsToAttack: true });
        AutoTestAssert.True(expectedAttackers >= 1, "At least one enemy intends to attack for FeelTheAudience");
        var fta = combatState.CreateCard<FeelTheAudience>(player);
        await CardPileCmd.Add(fta, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        e2Hp = enemy2.CurrentHp;
        cHp = cultist.CurrentHp;
        AutoTestAssert.True(fta.TryManualPlay(null), "Play FeelTheAudience");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 8, enemy1.CurrentHp, "FeelTheAudience dealt 8 to enemy1");
        AutoTestAssert.Equal(e2Hp - 8, enemy2.CurrentHp, "FeelTheAudience dealt 8 to enemy2");
        AutoTestAssert.Equal(cHp - 8, cultist.CurrentHp, "FeelTheAudience dealt 8 to cultist");
        AutoTestAssert.Equal(expectedAttackers, player.Creature.GetPowerAmount<WizPower>(), $"FeelTheAudience granted {expectedAttackers} Wiz for attacking enemies");
        await CleanPlayerBuffs();

        // 9. DaggerDance: deals 3x2 = 6 to ALL enemies!
        await ResetHandAndEnergy();
        var dd = combatState.CreateCard<DaggerDance>(player);
        await CardPileCmd.Add(dd, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        e2Hp = enemy2.CurrentHp;
        cHp = cultist.CurrentHp;
        AutoTestAssert.True(dd.TryManualPlay(null), "Play DaggerDance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "DaggerDance dealt 3x2 = 6 to enemy1");
        AutoTestAssert.Equal(e2Hp - 6, enemy2.CurrentHp, "DaggerDance dealt 3x2 = 6 to enemy2");
        AutoTestAssert.Equal(cHp - 6, cultist.CurrentHp, "DaggerDance dealt 3x2 = 6 to cultist");

        // 10. EdibleArmor + GremlinToss:
        //     Start with 12 Block -> EdibleArmor converts 12 Block -> 0 Block + 12 Temp HP, Exhausts!
        //     Gain 5 Block -> GremlinToss gains +3 Temp HP (total 15 Temp HP + 5 Block = 20 damage)!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx);
        await CreatureCmd.GainBlock(player.Creature, 12m, ValueProp.Unpowered, null);
        var edible = combatState.CreateCard<EdibleArmor>(player);
        await CardPileCmd.Add(edible, PileType.Hand);
        AutoTestAssert.True(edible.TryManualPlay(null), "Play EdibleArmor with 12 Block");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.Block, "EdibleArmor removed all 12 Block");
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "EdibleArmor converted 12 Block into 12 Temp HP");
        AutoTestAssert.Equal(PileType.Exhaust, edible.Pile?.Type, "EdibleArmor exhausted");

        await CreatureCmd.GainBlock(player.Creature, 5m, ValueProp.Unpowered, null);
        var toss = combatState.CreateCard<GremlinToss>(player);
        await CardPileCmd.Add(toss, PileType.Hand);
        AutoTestAssert.Equal(20m, toss.DynamicVars.CalculatedDamage.Calculate(enemy2), "GremlinToss previews 5 Block + 12 Temp HP + 3 gained = 20 damage");
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(toss.TryManualPlay(enemy2), "Play GremlinToss with 5 Block + 12 Temp HP (+3 -> 15)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(15, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "GremlinToss added +3 Temp HP (12 -> 15)");
        AutoTestAssert.Equal(e2Hp - 20, enemy2.CurrentHp, "GremlinToss dealt 5 Block + 15 Temp HP = 20 damage");
        await CleanPlayerBuffs();

        // 11. Enthusiasm (Swap -> draw 1):
        await ResetHandAndEnergy();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<GremlinDefend>(player), PileType.Draw, CardPilePosition.Top);
        var enth = combatState.CreateCard<Enthusiasm>(player);
        await CardPileCmd.Add(enth, PileType.Hand);
        AutoTestAssert.True(enth.TryManualPlay(null), "Play Enthusiasm");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnthusiasmPower>(), "EnthusiasmPower(1) active");
        await GremlinMob.SwapNextAsync(player, combatState, ctx);
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "EnthusiasmPower drew 1 card on Swap");

        // 12. Exacerbate (CrippledPower): deals 8 + applies CrippledPower(3); subsequent Weak application causes 3 unblockable HP loss!
        await ResetHandAndEnergy();
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx);
        var exac = combatState.CreateCard<Exacerbate>(player);
        await CardPileCmd.Add(exac, PileType.Hand);
        cHp = cultist.CurrentHp;
        AutoTestAssert.True(exac.TryManualPlay(cultist), "Play Exacerbate on cultist");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(cHp - 8, cultist.CurrentHp, "Exacerbate dealt 8 damage");
        AutoTestAssert.Equal(3, cultist.GetPowerAmount<CrippledPower>(), "Exacerbate applied CrippledPower(3)");
        AutoTestAssert.Equal(PileType.Exhaust, exac.Pile?.Type, "Exacerbate exhausted");

        await CreatureCmd.GainBlock(cultist, 20m, ValueProp.Unpowered, null);
        cHp = cultist.CurrentHp;
        await PowerCmd.Apply<WeakPower>(ctx, cultist, 1m, player.Creature, null);
        AutoTestAssert.Equal(cHp - 3, cultist.CurrentHp, "CrippledPower dealt 3 unblockable HP loss when Weak was applied");

        // 13. Fury (cost 3 -> 1 at 4 Strength, 5+4=9 x3 = 27 dmg) + IrksomeBlow (5 + 4x4 = 21 dmg) + GremlinOffensive+ (10 dmg + 4 Str = 14, Exhausts, adds Upgraded copy)!
        await ResetHandAndEnergy(energy: 6);
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, 4m, player.Creature, null);
        var fury = combatState.CreateCard<Fury>(player);
        await CardPileCmd.Add(fury, PileType.Hand);
        AutoTestAssert.Equal(1, fury.EnergyCost.GetWithModifiers(CostModifiers.All), "Fury discounted 3 -> 1 at 4 Strength");
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(fury.TryManualPlay(enemy2), "Play discounted Fury");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, pcs.Energy, "Fury spent 1 Energy (6 -> 5)");
        AutoTestAssert.Equal(e2Hp - 27, enemy2.CurrentHp, "Fury dealt (5+4)x3 = 27 damage");

        var irksome = combatState.CreateCard<IrksomeBlow>(player);
        await CardPileCmd.Add(irksome, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(irksome.TryManualPlay(enemy2), "Play IrksomeBlow at 4 Strength");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 21, enemy2.CurrentHp, "IrksomeBlow dealt 5 + 4x4 = 21 damage");

        var goffPlus = combatState.CreateCard<GremlinOffensive>(player);
        CardCmd.Upgrade(goffPlus);
        await CardPileCmd.Add(goffPlus, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(goffPlus.TryManualPlay(enemy2), "Play GremlinOffensive+ at 4 Strength");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 14, enemy2.CurrentHp, "GremlinOffensive+ dealt 10 + 4 = 14 damage");
        AutoTestAssert.Equal(PileType.Exhaust, goffPlus.Pile?.Type, "GremlinOffensive+ exhausted");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<GremlinOffensive>().Count(c => c.IsUpgraded), "GremlinOffensive+ added an Upgraded copy to Hand");
        await CleanPlayerBuffs();

        // 14. Cross-turn: InfiniteBlocks + Whiz (MakingMagicPower) + Whiz+ (MakingMoreMagicPower) + Bashed/BubbleBarrier expiration!
        await ResetHandAndEnergy();
        var bbExp = combatState.CreateCard<BubbleBarrier>(player);
        var infBlocks = combatState.CreateCard<InfiniteBlocks>(player);
        var whizBase = combatState.CreateCard<Whiz>(player);
        var whizPlus = combatState.CreateCard<Whiz>(player);
        CardCmd.Upgrade(whizPlus);
        await CardPileCmd.Add(bbExp, PileType.Hand);
        await CardPileCmd.Add(infBlocks, PileType.Hand);
        await CardPileCmd.Add(whizBase, PileType.Hand);
        await CardPileCmd.Add(whizPlus, PileType.Hand);
        AutoTestAssert.True(bbExp.TryManualPlay(null), "Play BubbleBarrier for expiration check");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<BubbleBarrierPower>(), "BubbleBarrierPower(2) active before Turn 1 end");
        AutoTestAssert.True(infBlocks.TryManualPlay(null), "Play InfiniteBlocks");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(whizBase.TryManualPlay(null), "Play Whiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(whizPlus.TryManualPlay(null), "Play Whiz+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<WizPower>(), "Whiz (1) + Whiz+ (2) = 3 Wiz");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MakingMagicPower>(), "Whiz applied MakingMagicPower(1)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MakingMoreMagicPower>(), "Whiz+ applied MakingMoreMagicPower(1)");

        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);

        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<BashedPower>(), "BashedPower expired at end of Turn 1");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<BubbleBarrierPower>(), "BubbleBarrierPower expired at end of Turn 1");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<MakingMagicPower>(), "MakingMagicPower expired on Turn 2 start");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<MakingMoreMagicPower>(), "MakingMoreMagicPower expired on Turn 2 start");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<Ward>().Count(), "InfiniteBlocksPower added 1 Ward on Turn 2 start");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<Bang>().Count(b => !b.IsUpgraded), "MakingMagicPower added 1 base Bang on Turn 2 start");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<Bang>().Count(b => b.IsUpgraded), "MakingMoreMagicPower added 1 Upgraded Bang+ on Turn 2 start");
    }

    /// <summary>
    /// End-to-end combat verification of the remaining 17 Gremlin Uncommon cards (Phase 3C-2)
    /// and their 4 custom powers (MakeshiftArmorPower, PolishPower, ScatterPower, WizardryPower).
    /// </summary>
    public static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGremlins>("DFGRM04");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var cultist = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.CalcifiedCultist>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 1200m);
        await CreatureCmd.GainMaxHp(enemy2, 1200m);
        await CreatureCmd.GainMaxHp(cultist, 1200m);

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

        async Task CleanPlayerBuffs()
        {
            if (player.Creature.GetPower<WizPower>() is { } w) await PowerCmd.Remove(w);
            if (player.Creature.GetPower<StrengthPower>() is { } s) await PowerCmd.Remove(s);
            if (player.Creature.GetPower<DownfallTempHpPower>() is { } t) await PowerCmd.Remove(t);
            if (player.Creature.GetPower<ArtifactPower>() is { } a) await PowerCmd.Remove(a);
            if (player.Creature.GetPower<AccuracyPower>() is { } acc) await PowerCmd.Remove(acc);
            if (player.Creature.GetPower<PolishPower>() is { } pol) await PowerCmd.Remove(pol);
            if (player.Creature.GetPower<MakeshiftArmorPower>() is { } ma) await PowerCmd.Remove(ma);
            if (player.Creature.GetPower<ScatterPower>() is { } sc) await PowerCmd.Remove(sc);
        }

        // Start as Mad Gremlin so Skill/Attack passives don't add unexpected Block/random hits.
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx);

        // 1. MakeshiftArmor (base + upgraded): countdown 7 Attacks -> grants stacked Artifact!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var maBase = combatState.CreateCard<MakeshiftArmor>(player);
        var maPlus = combatState.CreateCard<MakeshiftArmor>(player);
        CardCmd.Upgrade(maPlus);
        await CardPileCmd.Add(maBase, PileType.Hand);
        await CardPileCmd.Add(maPlus, PileType.Hand);
        AutoTestAssert.True(maBase.TryManualPlay(null), "Play MakeshiftArmor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ArtifactPower>(), "Base MakeshiftArmor grants 0 immediate Artifact");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MakeshiftArmorPower>(), "MakeshiftArmorPower(1) active");
        AutoTestAssert.Equal(7, player.Creature.GetPower<MakeshiftArmorPower>()!.DisplayAmount, "MakeshiftArmor starts at 7 attacks left");

        AutoTestAssert.True(maPlus.TryManualPlay(null), "Play MakeshiftArmor+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ArtifactPower>(), "MakeshiftArmor+ grants 1 immediate Artifact");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<MakeshiftArmorPower>(), "MakeshiftArmorPower stacked to 2 Artifact/trigger");

        for (int i = 0; i < 6; i++)
        {
            var shiv = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Shiv>(player);
            await CardPileCmd.Add(shiv, PileType.Hand);
            AutoTestAssert.True(shiv.TryManualPlay(enemy1), $"Play Attack #{i + 1} for MakeshiftArmor");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(1, player.Creature.GetPower<MakeshiftArmorPower>()!.DisplayAmount, "MakeshiftArmor at 1 attack left after 6 Attacks");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ArtifactPower>(), "Still 1 Artifact before 7th Attack");

        var shiv7 = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Shiv>(player);
        await CardPileCmd.Add(shiv7, PileType.Hand);
        AutoTestAssert.True(shiv7.TryManualPlay(enemy1), "Play 7th Attack for MakeshiftArmor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.GetPower<MakeshiftArmorPower>()!.DisplayAmount, "MakeshiftArmor reset countdown to 7");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ArtifactPower>(), "MakeshiftArmor granted +2 stacked Artifact (1 -> 3)");
        await CleanPlayerBuffs();

        // 2. Mockery: applies Weak; gains 9 Block only if enemy has >= 3 Weak after application!
        await ResetHandAndEnergy();
        var mockLow = combatState.CreateCard<Mockery>(player);
        await CardPileCmd.Add(mockLow, PileType.Hand);
        AutoTestAssert.True(mockLow.TryManualPlay(enemy1), "Play Mockery on 0-Weak enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "Mockery applied 1 Weak");
        AutoTestAssert.Equal(0, player.Creature.Block, "Mockery gained 0 Block at <3 Weak");

        await PowerCmd.Apply<WeakPower>(ctx, enemy1, 1m, player.Creature, null); // now 2 Weak
        var mockHigh = combatState.CreateCard<Mockery>(player);
        await CardPileCmd.Add(mockHigh, PileType.Hand);
        AutoTestAssert.True(mockHigh.TryManualPlay(enemy1), "Play Mockery on 2-Weak enemy1 (becomes 3 Weak)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, enemy1.GetPowerAmount<WeakPower>(), "Mockery raised Weak to 3");
        AutoTestAssert.Equal(9, player.Creature.Block, "Mockery gained 9 Block at >=3 Weak");
        if (enemy1.GetPower<WeakPower>() is { } wClean1) await PowerCmd.Remove(wClean1);

        // 3. PartyStick: Unplayable + Ethereal (removed on upgrade); Swap while in Hand -> +1 Energy; not in Hand -> 0!
        await ResetHandAndEnergy(energy: 3);
        var stick = combatState.CreateCard<PartyStick>(player);
        AutoTestAssert.True(stick.Keywords.Contains(CardKeyword.Unplayable), "PartyStick is Unplayable");
        AutoTestAssert.True(stick.Keywords.Contains(CardKeyword.Ethereal), "Base PartyStick is Ethereal");
        await CardPileCmd.Add(stick, PileType.Hand);
        AutoTestAssert.True(!stick.TryManualPlay(null), "PartyStick cannot be played");
        await GremlinMob.SwapNextAsync(player, combatState, ctx);
        AutoTestAssert.Equal(4, pcs.Energy, "PartyStick in Hand granted +1 Energy on Swap");

        CardCmd.Upgrade(stick);
        AutoTestAssert.True(!stick.Keywords.Contains(CardKeyword.Ethereal), "PartyStick+ loses Ethereal");
        await CardPileCmd.Add(stick, PileType.Discard);
        await GremlinMob.SwapNextAsync(player, combatState, ctx);
        AutoTestAssert.Equal(4, pcs.Energy, "PartyStick in Discard granted 0 Energy on Swap");
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx);

        // 4. Pickpocket: deals 10 dmg, gains +10 Gold, Exhausts, CanBeGeneratedInCombat == false!
        await ResetHandAndEnergy();
        var pick = combatState.CreateCard<Pickpocket>(player);
        AutoTestAssert.True(!pick.CanBeGeneratedInCombat, "Pickpocket cannot be generated in combat");
        await CardPileCmd.Add(pick, PileType.Hand);
        int goldBefore = player.Gold;
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(pick.TryManualPlay(enemy1), "Play Pickpocket");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 10, enemy1.CurrentHp, "Pickpocket dealt 10 damage");
        AutoTestAssert.Equal(goldBefore + 10, player.Gold, "Pickpocket gained +10 Gold");
        AutoTestAssert.Equal(PileType.Exhaust, pick.Pile?.Type, "Pickpocket exhausted");

        // 5. PinNeedle & PinNeedle+: deals 8/10 dmg, Exhausts, shuffles 2 base / 2 Upgraded Pinpricks into Draw Pile!
        await ResetHandAndEnergy();
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var needlePlus = combatState.CreateCard<PinNeedle>(player);
        CardCmd.Upgrade(needlePlus);
        await CardPileCmd.Add(needlePlus, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(needlePlus.TryManualPlay(enemy1), "Play PinNeedle+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 10, enemy1.CurrentHp, "PinNeedle+ dealt 10 damage");
        AutoTestAssert.Equal(PileType.Exhaust, needlePlus.Pile?.Type, "PinNeedle+ exhausted");
        AutoTestAssert.Equal(2, PileType.Draw.GetPile(player).Cards.OfType<Pinprick>().Count(p => p.IsUpgraded), "PinNeedle+ shuffled 2 Upgraded Pinpricks into Draw Pile");

        // 6. Polish: AccuracyPower(2) boosts Shiv (4->6) AND PolishPower(2) boosts Ward (3->5)!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var polish = combatState.CreateCard<Polish>(player);
        await CardPileCmd.Add(polish, PileType.Hand);
        AutoTestAssert.True(polish.TryManualPlay(null), "Play Polish");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<AccuracyPower>(), "Polish applied AccuracyPower(2)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<PolishPower>(), "Polish applied PolishPower(2)");

        var polShiv = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Shiv>(player);
        var polWard = combatState.CreateCard<Ward>(player);
        await CardPileCmd.Add(polShiv, PileType.Hand);
        await CardPileCmd.Add(polWard, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(polShiv.TryManualPlay(enemy1), "Play Shiv under Polish");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "Polished Shiv dealt 4 + 2 = 6 damage");
        AutoTestAssert.True(polWard.TryManualPlay(null), "Play Ward under Polish");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.Block, "Polished Ward granted 3 + 2 = 5 Block");
        await CleanPlayerBuffs();

        // 7. ProperTools: non-attacking cultist -> 3 Shivs; attacking Nibbit -> 3 Wards!
        await ResetHandAndEnergy();
        var ptCultist = combatState.CreateCard<ProperTools>(player);
        await CardPileCmd.Add(ptCultist, PileType.Hand);
        int cHp = cultist.CurrentHp;
        AutoTestAssert.True(ptCultist.TryManualPlay(cultist), "Play ProperTools on non-attacking cultist");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(cHp - 5, cultist.CurrentHp, "ProperTools dealt 5 damage to cultist");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.Shiv>().Count(), "ProperTools added 3 Shivs for non-attacking enemy");

        await ResetHandAndEnergy();
        var attackingEnemy = combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive && e.Monster is { IntendsToAttack: true });
        AutoTestAssert.NotNull(attackingEnemy, "At least one Nibbit intends to attack");
        var ptAtk = combatState.CreateCard<ProperTools>(player);
        await CardPileCmd.Add(ptAtk, PileType.Hand);
        int atkHp = attackingEnemy!.CurrentHp;
        AutoTestAssert.True(ptAtk.TryManualPlay(attackingEnemy), "Play ProperTools on attacking Nibbit");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(atkHp - 5, attackingEnemy.CurrentHp, "ProperTools dealt 5 damage to attacking Nibbit");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.OfType<Ward>().Count(), "ProperTools added 3 Wards for attacking enemy");

        // 8. RageBreak & RageBreak+: doubles Strength (3 -> 6 -> 12), + gains 5 Block, Exhausts!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, 3m, player.Creature, null);
        var rbBase = combatState.CreateCard<RageBreak>(player);
        await CardPileCmd.Add(rbBase, PileType.Hand);
        AutoTestAssert.True(rbBase.TryManualPlay(null), "Play RageBreak");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<StrengthPower>(), "RageBreak doubled Strength 3 -> 6");
        AutoTestAssert.Equal(0, player.Creature.Block, "Base RageBreak gained 0 Block");
        AutoTestAssert.Equal(PileType.Exhaust, rbBase.Pile?.Type, "RageBreak exhausted");

        var rbPlus = combatState.CreateCard<RageBreak>(player);
        CardCmd.Upgrade(rbPlus);
        await CardPileCmd.Add(rbPlus, PileType.Hand);
        AutoTestAssert.True(rbPlus.TryManualPlay(null), "Play RageBreak+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<StrengthPower>(), "RageBreak+ doubled Strength 6 -> 12");
        AutoTestAssert.Equal(5, player.Creature.Block, "RageBreak+ gained 5 Block");
        AutoTestAssert.Equal(PileType.Exhaust, rbPlus.Pile?.Type, "RageBreak+ exhausted");
        await CleanPlayerBuffs();

        // 9. Raid (draws 5 for 5 living Gremlins, discards 3 -> 2 in hand) + Revel (costs 3, gains 5 Energy -> net +2)!
        await ResetHandAndEnergy(energy: 6);
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<GremlinDefend>(player), PileType.Draw, CardPilePosition.Top);
        var raid = combatState.CreateCard<Raid>(player);
        await CardPileCmd.Add(raid, PileType.Hand);
        AutoTestAssert.True(raid.TryManualPlay(null), "Play Raid");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Raid drew 5 and discarded 3 -> 2 cards remaining in Hand");

        await ResetHandAndEnergy(energy: 3);
        var revel = combatState.CreateCard<Revel>(player);
        await CardPileCmd.Add(revel, PileType.Hand);
        AutoTestAssert.True(revel.TryManualPlay(null), "Play Revel at 3 Energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, pcs.Energy, "Revel spent 3 and gained 5 Energy for 5 living Gremlins");

        // 10. Rhythm: Swaps to next Gremlin + fetches a Basic card from Draw Pile into Hand at 0 cost (via grid picker)!
        await ResetHandAndEnergy();
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var basicStrike = combatState.CreateCard<GremlinStrike>(player);
        var basicDefend = combatState.CreateCard<GremlinDefend>(player);
        var nonBasic = combatState.CreateCard<Kablamo>(player);
        await CardPileCmd.Add(basicStrike, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(basicDefend, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(nonBasic, PileType.Draw, CardPilePosition.Top);
        var kindBeforeRhythm = GremlinMob.GetActiveKind(combatState, player);
        var rhythm = combatState.CreateCard<Rhythm>(player);
        await CardPileCmd.Add(rhythm, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, basicStrike)))
        {
            AutoTestAssert.True(rhythm.TryManualPlay(null), "Play Rhythm");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(GremlinMob.GetActiveKind(combatState, player) != kindBeforeRhythm, "Rhythm swapped active Gremlin");
        AutoTestAssert.Equal(PileType.Hand, basicStrike.Pile?.Type, "Rhythm fetched Basic card into Hand");
        AutoTestAssert.Equal(0, basicStrike.EnergyCost.GetWithModifiers(CostModifiers.All), "Fetched Basic card costs 0 this turn");
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx);

        // 11. Scatter + Temp HP deterministic layering (Temp HP absorbs first; Scatter prevents next HP loss & Swaps randomly)!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        var scatter = combatState.CreateCard<Scatter>(player);
        await CardPileCmd.Add(scatter, PileType.Hand);
        AutoTestAssert.True(scatter.TryManualPlay(null), "Play Scatter");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ScatterPower>(), "ScatterPower(1) active");
        AutoTestAssert.Equal(PileType.Exhaust, scatter.Pile?.Type, "Scatter exhausted");

        await PowerCmd.Apply<DownfallTempHpPower>(ctx, player.Creature, 4m, player.Creature, null);
        int hpBefore = player.Creature.CurrentHp;
        var kindBeforeScatter = GremlinMob.GetActiveKind(combatState, player);
        await CreatureCmd.Damage(ctx, player.Creature, 4m, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.Equal(hpBefore, player.Creature.CurrentHp, "Temp HP absorbed 4 damage (0 HP lost)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DownfallTempHpPower>(), "Temp HP depleted");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ScatterPower>(), "ScatterPower was NOT consumed while Temp HP absorbed damage");

        await CreatureCmd.Damage(ctx, player.Creature, 10m, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.Equal(hpBefore, player.Creature.CurrentHp, "ScatterPower prevented 10 HP loss");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ScatterPower>(), "ScatterPower decremented to 0");
        AutoTestAssert.True(GremlinMob.GetActiveKind(combatState, player) != kindBeforeScatter, "ScatterPower swapped to a random Gremlin");
        await GremlinMob.SwapToSpecificAsync(player, combatState, GremlinKind.Mad, ctx);

        // 12. SharpenBlades+: draws 1 card, sets Attacks in Hand to 0 cost, leaves Skills untouched!
        await ResetHandAndEnergy();
        var drawnAtk = combatState.CreateCard<Pickpocket>(player); // 2-cost Attack
        await CardPileCmd.Add(drawnAtk, PileType.Draw, CardPilePosition.Top);
        var handAtk = combatState.CreateCard<ProperTools>(player); // 2-cost Attack
        var handSkill = combatState.CreateCard<Revel>(player); // 3-cost Skill
        var sharpenPlus = combatState.CreateCard<SharpenBlades>(player);
        CardCmd.Upgrade(sharpenPlus);
        await CardPileCmd.Add(handAtk, PileType.Hand);
        await CardPileCmd.Add(handSkill, PileType.Hand);
        await CardPileCmd.Add(sharpenPlus, PileType.Hand);
        AutoTestAssert.True(sharpenPlus.TryManualPlay(null), "Play SharpenBlades+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(PileType.Hand, drawnAtk.Pile?.Type, "SharpenBlades+ drew 1 card first");
        AutoTestAssert.Equal(0, drawnAtk.EnergyCost.GetWithModifiers(CostModifiers.All), "SharpenBlades+ reduced drawn Attack to 0");
        AutoTestAssert.Equal(0, handAtk.EnergyCost.GetWithModifiers(CostModifiers.All), "SharpenBlades+ reduced in-hand Attack to 0");
        AutoTestAssert.Equal(3, handSkill.EnergyCost.GetWithModifiers(CostModifiers.All), "SharpenBlades+ left Skill cost unchanged");

        // 13. ShowOfHands+: draws 1 card, gains 2 Block per card in Hand!
        await ResetHandAndEnergy();
        await CardPileCmd.Add(combatState.CreateCard<GremlinDefend>(player), PileType.Draw, CardPilePosition.Top);
        for (int i = 0; i < 3; i++)
            await CardPileCmd.Add(combatState.CreateCard<GremlinDefend>(player), PileType.Hand);
        var sohPlus = combatState.CreateCard<ShowOfHands>(player);
        CardCmd.Upgrade(sohPlus);
        await CardPileCmd.Add(sohPlus, PileType.Hand);
        AutoTestAssert.True(sohPlus.TryManualPlay(null), "Play ShowOfHands+ with 3 other cards + 1 drawn = 4 cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "ShowOfHands+ granted 4 x 2 = 8 Block");

        // 14. SleeveOfAces+ at 3 Wiz: deals 5+7=12 dmg, adds 3 Upgraded Shivs, consumes Wiz!
        await ResetHandAndEnergy();
        await CleanPlayerBuffs();
        await PowerCmd.Apply<WizPower>(ctx, player.Creature, 3m, player.Creature, null);
        var sleevePlus = combatState.CreateCard<SleeveOfAces>(player);
        CardCmd.Upgrade(sleevePlus);
        await CardPileCmd.Add(sleevePlus, PileType.Hand);
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(sleevePlus.TryManualPlay(enemy2), "Play SleeveOfAces+ at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 12, enemy2.CurrentHp, "SleeveOfAces+ dealt 5 + 7 = 12 damage");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.Shiv>().Count(s => s.IsUpgraded), "SleeveOfAces+ added 3 Upgraded Shivs");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "SleeveOfAces+ consumed 3 Wiz");

        // 15. Stupend at 3 Wiz: gains 7+7=14 Block AND deals 7+7=14 damage, consumes Wiz!
        await ResetHandAndEnergy();
        await PowerCmd.Apply<WizPower>(ctx, player.Creature, 3m, player.Creature, null);
        var stupend = combatState.CreateCard<Stupend>(player);
        await CardPileCmd.Add(stupend, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(stupend.TryManualPlay(enemy2), "Play Stupend at 3 Wiz");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(14, player.Creature.Block, "Stupend at 3 Wiz gained 7 + 7 = 14 Block");
        AutoTestAssert.Equal(e2Hp - 14, enemy2.CurrentHp, "Stupend at 3 Wiz dealt 7 + 7 = 14 damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WizPower>(), "Stupend consumed 3 Wiz");

        // 16. Wizardry+ (Innate) -> Turn 2 start grants +1 Wiz!
        await ResetHandAndEnergy();
        var wizCard = combatState.CreateCard<Wizardry>(player);
        AutoTestAssert.True(!wizCard.Keywords.Contains(CardKeyword.Innate), "Base Wizardry is not Innate");
        CardCmd.Upgrade(wizCard);
        AutoTestAssert.True(wizCard.Keywords.Contains(CardKeyword.Innate), "Wizardry+ is Innate");
        await CardPileCmd.Add(wizCard, PileType.Hand);
        AutoTestAssert.True(wizCard.TryManualPlay(null), "Play Wizardry+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WizardryPower>(), "WizardryPower(1) active");

        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WizPower>(), "WizardryPower granted +1 Wiz on Turn 2 start");
    }
}
