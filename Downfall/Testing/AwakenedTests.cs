using Downfall.Awakened;
using Downfall.Awakened.Cards;
using Downfall.Awakened.Powers;
using Downfall.Awakened.Relics;
using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Testing;

internal static class AwakenedTests
{
    public static Task TestVisualsRuntime() =>
        DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheAwakenedOne>("DFAWKVIS");

    public static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAwakenedOne>("DFAWK01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy, 400m);

        // 1. Verify starting loadout & Turn 1 RippedDoll Conjure!
        AutoTestAssert.Equal(70, player.Creature.MaxHp, "Awakened One starts with 70 MaxHp");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Awakened One starts with 10-card deck");
        AutoTestAssert.True(player.Relics.OfType<RippedDoll>().Any(), "Awakened One starts with RippedDoll");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<AwakenedSpellCard>().Any(), "RippedDoll conjured a Spell on Turn 1");
        AutoTestAssert.Equal(1, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "1 Conjure on Turn 1 start");

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 2. TalonRake: deals 5 damage twice (10) and Conjures!
        await ResetHandAndEnergy();
        var talon = combatState.CreateCard<TalonRake>(player);
        await CardPileCmd.Add(talon, PileType.Hand);
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(talon.TryManualPlay(enemy), "Play TalonRake");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 10, enemy.CurrentHp, "TalonRake dealt 5x2 = 10 damage");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<AwakenedSpellCard>().Any(), "TalonRake conjured a Spell into Hand");

        // Conjure 2 more times -> all 4 default spells seen without repeats in the first cycle!
        var spell3 = await AwakenedSpellbook.ConjureAsync(player, combatState);
        var spell4 = await AwakenedSpellbook.ConjureAsync(player, combatState);
        AutoTestAssert.NotNull(spell3, "3rd Conjure produced a spell");
        AutoTestAssert.NotNull(spell4, "4th Conjure produced a spell");
        var seenTypes = pcs.AllCards.OfType<AwakenedSpellCard>().Select(c => c.GetType()).Distinct().Count();
        AutoTestAssert.Equal(4, seenTypes, "First 4 Conjures cycle through all 4 distinct Spellbook spells");

        // 3. Spell cards & Manaburn (Void draw + Hymn Drained)!
        await ResetHandAndEnergy();
        var darkleech = combatState.CreateCard<Darkleech>(player);
        var cryostasis = combatState.CreateCard<Cryostasis>(player);
        var burning = combatState.CreateCard<BurningStudy>(player);
        var bolt = combatState.CreateCard<Thunderbolt>(player);
        var esp = combatState.CreateCard<EspSpell>(player);
        await CardPileCmd.Add(darkleech, PileType.Hand);
        await CardPileCmd.Add(cryostasis, PileType.Hand);
        await CardPileCmd.Add(burning, PileType.Hand);
        await CardPileCmd.Add(bolt, PileType.Hand);
        await CardPileCmd.Add(esp, PileType.Hand);

        AutoTestAssert.True(darkleech.TryManualPlay(enemy), "Play Darkleech");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Darkleech applied 1 Vulnerable");
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<ManaburnPower>(), "Darkleech applied 4 Manaburn");

        // Void drawn at 0 Energy still triggers Manaburn (4 HP loss to enemy)!
        await PlayerCmd.SetEnergy(0, player);
        var voidCard = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(voidCard, PileType.Draw, CardPilePosition.Top);
        hpBefore = enemy.CurrentHp;
        await CardPileCmd.Draw(ctx, 1, player);
        AutoTestAssert.Equal(hpBefore - 4, enemy.CurrentHp, "Drawing Void at 0 Energy still triggered Manaburn for 4 HP loss");
        await PlayerCmd.GainEnergy(10, player);

        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(cryostasis.TryManualPlay(null), "Play Cryostasis");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 10, player.Creature.Block, "Cryostasis gained 10 Block");

        AutoTestAssert.True(burning.TryManualPlay(null), "Play BurningStudy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "BurningStudy granted 1 Strength");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "BurningStudy applied 1 Weak");

        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(bolt.TryManualPlay(enemy), "Play Thunderbolt (+1 Str, enemy Vuln -> floor(13*1.5)=19)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 19, enemy.CurrentHp, "Thunderbolt dealt 19 damage");

        int handBeforeEsp = pcs.Hand.Cards.Count;
        AutoTestAssert.True(esp.TryManualPlay(null), "Play EspSpell");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBeforeEsp, pcs.Hand.Cards.Count, "EspSpell exhausted (-1) and drew 1 card (net 0)");

        // 4. Hymn (x2 for multi-stack Drained!) + Ceremony + Chant check + Awakening at 7 Powers (with cross-pool Inflame as 7th)!
        await ResetHandAndEnergy();
        blockBefore = player.Creature.Block;
        var hymn = combatState.CreateCard<Hymn>(player);
        var hymn2 = combatState.CreateCard<Hymn>(player);
        await CardPileCmd.Add(hymn, PileType.Hand);
        await CardPileCmd.Add(hymn2, PileType.Hand);
        AutoTestAssert.True(hymn.TryManualPlay(null), "Play Hymn #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(hymn2.TryManualPlay(null), "Play Hymn #2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 6, player.Creature.Block, "2x Hymn gained 6 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<HermitDrainedPower>(), "2x Hymn applied 2 Drained");
        AutoTestAssert.True(!AwakenedCard.WasLastCardPlayedPower(player, combatState), "Chant is false after non-Power (Hymn)");
        var ceremony = pcs.Hand.Cards.OfType<Ceremony>().FirstOrDefault();
        AutoTestAssert.NotNull(ceremony, "Hymn added Ceremony to Hand");

        // Hold an unupgraded spell in hand to verify Awakening upgrades it in place!
        var waitingSpell = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(waitingSpell, PileType.Hand);
        AutoTestAssert.True(!waitingSpell.IsUpgraded, "Spell starts unupgraded before Awakening");

        // Play 6 Ceremonies + 1 cross-pool Inflame = 7 Powers -> triggers Awakening!
        int strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(ceremony!.TryManualPlay(null), "Play Ceremony #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(strBefore + 1, player.Creature.GetPowerAmount<StrengthPower>(), "Ceremony granted +1 Strength");
        AutoTestAssert.True(AwakenedCard.WasLastCardPlayedPower(player, combatState), "Chant detects previous card was a Power");

        for (int i = 0; i < 5; i++)
        {
            var extraCeremony = combatState.CreateCard<Ceremony>(player);
            await CardPileCmd.Add(extraCeremony, PileType.Hand);
            AutoTestAssert.True(extraCeremony.TryManualPlay(null), $"Play Ceremony #{i + 2}");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(!AwakenedSpellbook.IsAwakened(combatState, player), "Not yet Awakened after 6 Powers");
        var inflame = combatState.CreateCard<StsCards.Inflame>(player);
        await CardPileCmd.Add(inflame, PileType.Hand);
        AutoTestAssert.True(inflame.TryManualPlay(null), "Play non-Awakened Inflame as 7th Power");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(AwakenedSpellbook.IsAwakened(combatState, player), "Awakened after playing 7th Power (cross-pool Inflame)");
        AutoTestAssert.True(waitingSpell.IsUpgraded, "Awakening upgraded existing Spell in Hand");

        // Verify AddSpellToSpellbook preserves default cycle + includes EspSpell.
        AwakenedSpellbook.AddSpellToSpellbook(combatState, player, ModelDb.Card<EspSpell>());

        // Turn 2: RippedDoll conjures an UPGRADED Spell, and 2x Hymn Drained loses 2 Energy AND triggers Manaburn (2*4 = 8 HP loss)!
        await CreatureCmd.GainBlock(player.Creature, 40m, ValueProp.Unpowered, null);
        hpBefore = enemy.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(player.MaxEnergy - 2, pcs.Energy, "2x Hymn Drained reduced Round 2 Energy by 2");
        AutoTestAssert.Equal(hpBefore - 8, enemy.CurrentHp, "2x Hymn Drained triggered Manaburn for 2*4 = 8 HP loss on Round 2 start");
        var turn2Spells = pcs.Hand.Cards.OfType<AwakenedSpellCard>().ToList();
        AutoTestAssert.True(turn2Spells.Count >= 1 && turn2Spells.All(s => s.IsUpgraded), "RippedDoll Turn 2 Conjure produced upgraded Spell after Awakening");

        var rippedDoll = player.Relics.OfType<RippedDoll>().First();
        AutoTestAssert.Equal(RelicStatus.Disabled, rippedDoll.Status, "RippedDoll disabled after 2 turns");
        AutoTestAssert.True(!rippedDoll.ShowCounter, "RippedDoll hides counter once exhausted");

        // Turn 3: RippedDoll must NOT conjure again!
        int conjuresAfterRound2 = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        await CreatureCmd.GainBlock(player.Creature, 40m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(conjuresAfterRound2, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "RippedDoll did NOT conjure on Turn 3");
    }

    public static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAwakenedOne>("DFAWKC1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 1. Brainshock: 12 damage + adds Void to Discard Pile.
        await ResetHandAndEnergy();
        var brainshock = combatState.CreateCard<Brainshock>(player);
        await CardPileCmd.Add(brainshock, PileType.Hand);
        int hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(brainshock.TryManualPlay(enemy1), "Play Brainshock");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 12, enemy1.CurrentHp, "Brainshock dealt 12 damage");
        AutoTestAssert.True(PileType.Discard.GetPile(player).Cards.Any(c => c is StsCards.Void), "Brainshock added Void to Discard");

        // 2. Scour + ClarionCall + Spew + VoidRefundPower at 0 Energy on Void draw!
        //    Scour deals 7 + applies 3 Manaburn; ClarionCall deals 8 + applies VoidRefundPower(1);
        //    Spew deals 6 + 1 EnergySpent triggers BOTH Manaburn (3 HP loss) AND VoidRefundPower (+1 Energy)!
        await ResetHandAndEnergy();
        var scour = combatState.CreateCard<Scour>(player);
        var clarion = combatState.CreateCard<ClarionCall>(player);
        var spew = combatState.CreateCard<Spew>(player);
        await CardPileCmd.Add(scour, PileType.Hand);
        await CardPileCmd.Add(clarion, PileType.Hand);
        await CardPileCmd.Add(spew, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(scour.TryManualPlay(enemy1), "Play Scour");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 7, enemy1.CurrentHp, "Scour dealt 7 damage");
        AutoTestAssert.Equal(3, enemy1.GetPowerAmount<ManaburnPower>(), "Scour applied 3 Manaburn");

        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(clarion.TryManualPlay(enemy1), "Play ClarionCall");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 8, enemy1.CurrentHp, "ClarionCall dealt 8 damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<VoidRefundPower>(), "ClarionCall applied 1 VoidRefundPower");

        int energyBeforeSpew = pcs.Energy;
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(spew.TryManualPlay(enemy1), "Play Spew");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6 - 3, enemy1.CurrentHp, "Spew dealt 6 damage + triggered Manaburn for 3 HP loss");
        AutoTestAssert.Equal(energyBeforeSpew, pcs.Energy, "VoidRefundPower refunded 1 Energy on Spew drain");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<VoidRefundPower>(), "VoidRefundPower consumed");

        // 2b. 0-cost Spew spends 0 Energy -> must NOT trigger Manaburn or VoidRefundPower!
        await PowerCmd.Apply<VoidRefundPower>(ctx, player.Creature, 1m, player.Creature, null);
        var freeSpew = combatState.CreateCard<Spew>(player);
        freeSpew.EnergyCost.SetThisTurn(0);
        await CardPileCmd.Add(freeSpew, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(freeSpew.TryManualPlay(enemy1), "Play 0-cost Spew");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy1.CurrentHp, "0-cost Spew dealt 6 damage without triggering Manaburn");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<VoidRefundPower>(), "0-cost Spew did NOT consume VoidRefundPower");

        // 2c. Void drawn at 0 Energy with VoidRefundPower(1) -> Void loses energy first (0 -> 0), THEN VoidRefundPower refunds to 1!
        await PlayerCmd.SetEnergy(0, player);
        var refundVoid = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(refundVoid, PileType.Draw, CardPilePosition.Top);
        hpBefore = enemy1.CurrentHp;
        await CardPileCmd.Draw(ctx, 1, player);
        AutoTestAssert.Equal(1, pcs.Energy, "VoidRefundPower refunded to 1 Energy AFTER Void energy loss at 0 Energy");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<VoidRefundPower>(), "VoidRefundPower consumed by Void draw");
        AutoTestAssert.Equal(hpBefore - 3, enemy1.CurrentHp, "Void draw also triggered Manaburn for 3 HP loss");
        await PowerCmd.Remove<ManaburnPower>(enemy1);

        // 3. Clutch: 8 damage + draws a 0-cost card from Draw Pile (respecting NoDrawPower!).
        await ResetHandAndEnergy();
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var zeroCard = combatState.CreateCard<Hymn>(player);
        var oneCard = combatState.CreateCard<AwakenedStrike>(player);
        await CardPileCmd.Add(oneCard, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(zeroCard, PileType.Draw, CardPilePosition.Bottom);
        await PowerCmd.Apply<NoDrawPower>(ctx, player.Creature, 1m, player.Creature, null);
        var clutchBlocked = combatState.CreateCard<Clutch>(player);
        await CardPileCmd.Add(clutchBlocked, PileType.Hand);
        AutoTestAssert.True(clutchBlocked.TryManualPlay(enemy1), "Play Clutch under NoDrawPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(zeroCard), "Clutch respected NoDrawPower and did not draw");
        await PowerCmd.Remove<NoDrawPower>(player.Creature);

        var clutch = combatState.CreateCard<Clutch>(player);
        await CardPileCmd.Add(clutch, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(clutch.TryManualPlay(enemy1), "Play Clutch");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 8, enemy1.CurrentHp, "Clutch dealt 8 damage");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(zeroCard), "Clutch drew 0-cost card from Draw Pile");

        // 4. Dejection: 7 damage + exhausts a Spell -> adds Ceremony; exhausting non-Spell -> NO Ceremony!
        await ResetHandAndEnergy();
        var dejection = combatState.CreateCard<Dejection>(player);
        var spellFodder = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(dejection, PileType.Hand);
        await CardPileCmd.Add(spellFodder, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, spellFodder)))
        {
            AutoTestAssert.True(dejection.TryManualPlay(enemy1), "Play Dejection exhausting Spell");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(hpBefore - 7, enemy1.CurrentHp, "Dejection dealt 7 damage");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(spellFodder), "Dejection exhausted Spell");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<Ceremony>().Any(), "Dejection added Ceremony on Spell exhaust");

        await ResetHandAndEnergy();
        var dejection2 = combatState.CreateCard<Dejection>(player);
        var nonSpellFodder = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(dejection2, PileType.Hand);
        await CardPileCmd.Add(nonSpellFodder, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, nonSpellFodder)))
        {
            AutoTestAssert.True(dejection2.TryManualPlay(enemy1), "Play Dejection exhausting non-Spell");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(nonSpellFodder), "Dejection exhausted non-Spell");
        AutoTestAssert.True(!pcs.Hand.Cards.OfType<Ceremony>().Any(), "Dejection did NOT add Ceremony on non-Spell exhaust");

        // 5. Envision: 4 Block + Conjures a Spell on TOP of Draw Pile!
        await ResetHandAndEnergy();
        int blockBefore = player.Creature.Block;
        var envision = combatState.CreateCard<Envision>(player);
        await CardPileCmd.Add(envision, PileType.Hand);
        AutoTestAssert.True(envision.TryManualPlay(null), "Play Envision");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 4, player.Creature.Block, "Envision gained 4 Block");
        AutoTestAssert.True(PileType.Draw.GetPile(player).Cards.FirstOrDefault() is AwakenedSpellCard, "Envision put Conjured Spell on top of Draw Pile");

        // 6. Gloomguard: costs 1 normally, costs 0 with Void in Hand, reverts to 1 after Void leaves Hand!
        await ResetHandAndEnergy();
        var gloom = combatState.CreateCard<Gloomguard>(player);
        await CardPileCmd.Add(gloom, PileType.Hand);
        AutoTestAssert.Equal(1, gloom.EnergyCost.GetWithModifiers(CostModifiers.All), "Gloomguard costs 1 without Void in Hand");
        var handVoid = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(handVoid, PileType.Hand);
        AutoTestAssert.Equal(0, gloom.EnergyCost.GetWithModifiers(CostModifiers.All), "Gloomguard costs 0 with Void in Hand");
        await CardPileCmd.Add(handVoid, PileType.Discard);
        AutoTestAssert.Equal(1, gloom.EnergyCost.GetWithModifiers(CostModifiers.All), "Gloomguard reverted to cost 1 after Void left Hand");
        await CardPileCmd.Add(handVoid, PileType.Hand);
        blockBefore = player.Creature.Block;
        int energyBeforeGloom = pcs.Energy;
        AutoTestAssert.True(gloom.TryManualPlay(null), "Play 0-cost Gloomguard");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "Gloomguard gained 8 Block");
        AutoTestAssert.Equal(energyBeforeGloom, pcs.Energy, "0-cost Gloomguard spent 0 Energy");

        // 7. Initiation (11 Block + Ceremony) + Chant negative & positive paths (Recitation, Gather, FlareShot)!
        //    First verify negative Chant paths (previous card was Gloomguard, NOT a Power):
        await ResetHandAndEnergy();
        var nonChantRecit = combatState.CreateCard<Recitation>(player);
        var nonChantGather = combatState.CreateCard<Gather>(player);
        var nonChantFlare = combatState.CreateCard<FlareShot>(player);
        await CardPileCmd.Add(nonChantRecit, PileType.Hand);
        await CardPileCmd.Add(nonChantGather, PileType.Hand);
        await CardPileCmd.Add(nonChantFlare, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(nonChantRecit.TryManualPlay(enemy1), "Play non-Chant Recitation");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy1.CurrentHp, "Non-Chant Recitation dealt 6 once");
        int handBeforeNonChantGather = pcs.Hand.Cards.Count;
        AutoTestAssert.True(nonChantGather.TryManualPlay(null), "Play non-Chant Gather");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBeforeNonChantGather - 1, pcs.Hand.Cards.Count, "Non-Chant Gather did NOT pull from Discard");
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(nonChantFlare.TryManualPlay(enemy1), "Play non-Chant FlareShot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 4, enemy1.CurrentHp, "Non-Chant FlareShot dealt 4 damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "Non-Chant FlareShot did NOT apply DrawCardsNextTurnPower");

        await ResetHandAndEnergy();
        blockBefore = player.Creature.Block;
        var initiation = combatState.CreateCard<Initiation>(player);
        await CardPileCmd.Add(initiation, PileType.Hand);
        AutoTestAssert.True(initiation.TryManualPlay(null), "Play Initiation");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 11, player.Creature.Block, "Initiation gained 11 Block");
        var ceremony = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<Ceremony>().FirstOrDefault(), "Initiation added Ceremony");

        // Play Ceremony (Power -> +1 Str), then Recitation (Chant triggers -> hits twice for (6+1)*2 = 14)!
        var recitation = combatState.CreateCard<Recitation>(player);
        await CardPileCmd.Add(recitation, PileType.Hand);
        AutoTestAssert.True(ceremony.TryManualPlay(null), "Play Ceremony before Recitation");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(recitation.TryManualPlay(enemy1), "Play Chanted Recitation #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 14, enemy1.CurrentHp, "Chanted Recitation dealt (6+1)*2 = 14 damage");

        // Replay the SAME Recitation after a non-Power (Recitation itself!) -> persistent Chant still hits twice!
        await CardPileCmd.Add(recitation, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(recitation.TryManualPlay(enemy1), "Play persistently Chanted Recitation #2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 14, enemy1.CurrentHp, "Persistent Chant kept Recitation hitting twice for 14");

        // Play Ceremony -> Gather (Chant returns preferred card from Discard Pile to Hand)!
        await ResetHandAndEnergy();
        var discardTarget = combatState.CreateCard<Peck>(player);
        await CardPileCmd.Add(discardTarget, PileType.Discard);
        var cer2 = combatState.CreateCard<Ceremony>(player);
        var gather = combatState.CreateCard<Gather>(player);
        await CardPileCmd.Add(cer2, PileType.Hand);
        await CardPileCmd.Add(gather, PileType.Hand);
        AutoTestAssert.True(cer2.TryManualPlay(null), "Play Ceremony before Gather");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, discardTarget)))
        {
            AutoTestAssert.True(gather.TryManualPlay(null), "Play Chanted Gather");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 3, player.Creature.Block, "Gather gained 3 Block");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(discardTarget), "Chanted Gather returned card from Discard to Hand");

        // Play Ceremony -> FlareShot (Chant applies DrawCardsNextTurnPower(1) and deals 4+Str damage)!
        await ResetHandAndEnergy();
        var cer3 = combatState.CreateCard<Ceremony>(player);
        var flare = combatState.CreateCard<FlareShot>(player);
        await CardPileCmd.Add(cer3, PileType.Hand);
        await CardPileCmd.Add(flare, PileType.Hand);
        AutoTestAssert.True(cer3.TryManualPlay(null), "Play Ceremony before FlareShot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int strNow = player.Creature.GetPowerAmount<StrengthPower>();
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(flare.TryManualPlay(enemy1), "Play Chanted FlareShot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - (4 + strNow), enemy1.CurrentHp, "Chanted FlareShot dealt 4+Str damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "Chanted FlareShot applied 1 DrawCardsNextTurnPower");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 8. Peck: 5 Block, 1 damage, draw 1 card.
        await ResetHandAndEnergy();
        await CardPileCmd.Add(combatState.CreateCard<AwakenedDefend>(player), PileType.Draw, CardPilePosition.Top);
        var peck = combatState.CreateCard<Peck>(player);
        await CardPileCmd.Add(peck, PileType.Hand);
        blockBefore = player.Creature.Block;
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(peck.TryManualPlay(enemy1), "Play Peck");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 5, player.Creature.Block, "Peck gained 5 Block");
        AutoTestAssert.Equal(hpBefore - 1, enemy1.CurrentHp, "Peck dealt 1 damage");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "Peck drew 1 card");

        // 9. Planeswalk: +2 Energy + shuffles Void into Draw Pile.
        await ResetHandAndEnergy();
        int voidsBefore = PileType.Draw.GetPile(player).Cards.Count(c => c is StsCards.Void);
        int energyBeforePw = pcs.Energy;
        var pw = combatState.CreateCard<Planeswalk>(player);
        await CardPileCmd.Add(pw, PileType.Hand);
        AutoTestAssert.True(pw.TryManualPlay(null), "Play Planeswalk");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforePw + 2, pcs.Energy, "Planeswalk gained 2 Energy");
        AutoTestAssert.Equal(voidsBefore + 1, PileType.Draw.GetPile(player).Cards.Count(c => c is StsCards.Void), "Planeswalk shuffled Void into Draw Pile");

        // 10. Pluck + PlumeJab: Pluck deals 2 AOE + adds PlumeJab; PlumeJab deals 2x2 = 4 random damage and exhausts!
        await ResetHandAndEnergy();
        var pluck = combatState.CreateCard<Pluck>(player);
        await CardPileCmd.Add(pluck, PileType.Hand);
        int e1Before = enemy1.CurrentHp;
        int e2Before = enemy2.CurrentHp;
        AutoTestAssert.True(pluck.TryManualPlay(null), "Play Pluck");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 2, enemy1.CurrentHp, "Pluck dealt 2 to enemy1");
        AutoTestAssert.Equal(e2Before - 2, enemy2.CurrentHp, "Pluck dealt 2 to enemy2");
        var plumeJab = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<PlumeJab>().FirstOrDefault(), "Pluck added PlumeJab");
        int totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(plumeJab.TryManualPlay(null), "Play PlumeJab");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 4, enemy1.CurrentHp + enemy2.CurrentHp, "PlumeJab dealt 2x2 = 4 total damage");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(plumeJab), "PlumeJab exhausted");

        // 11. ProfaneStrike: 11 damage + puts chosen card from Hand on top of Draw Pile.
        await ResetHandAndEnergy();
        var profane = combatState.CreateCard<ProfaneStrike>(player);
        var topDeckCard = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(profane, PileType.Hand);
        await CardPileCmd.Add(topDeckCard, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topDeckCard)))
        {
            AutoTestAssert.True(profane.TryManualPlay(enemy1), "Play ProfaneStrike");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(hpBefore - 11, enemy1.CurrentHp, "ProfaneStrike dealt 11 damage");
        AutoTestAssert.True(ReferenceEquals(PileType.Draw.GetPile(player).Cards.FirstOrDefault(), topDeckCard), "ProfaneStrike put card on top of Draw Pile");

        // 12. Psalm: 10 AOE damage + 1 Weak to ALL enemies.
        await ResetHandAndEnergy();
        var psalm = combatState.CreateCard<Psalm>(player);
        await CardPileCmd.Add(psalm, PileType.Hand);
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;
        AutoTestAssert.True(psalm.TryManualPlay(null), "Play Psalm");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 10, enemy1.CurrentHp, "Psalm dealt 10 to enemy1");
        AutoTestAssert.Equal(e2Before - 10, enemy2.CurrentHp, "Psalm dealt 10 to enemy2");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "Psalm applied 1 Weak to enemy1");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<WeakPower>(), "Psalm applied 1 Weak to enemy2");

        // 13. Unleash: 5 + 1 * (other cards in hand = 3) = 8 damage!
        await ResetHandAndEnergy();
        var unleash = combatState.CreateCard<Unleash>(player);
        await CardPileCmd.Add(unleash, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<AwakenedDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<AwakenedDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<AwakenedDefend>(player), PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(unleash.TryManualPlay(enemy1), "Play Unleash with 3 other cards in hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 8, enemy1.CurrentHp, "Unleash dealt 5 + 3 = 8 damage");
    }

    public static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAwakenedOne>("DFAWK03");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

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

        // 1. Altar: gains 5 Block, exhausts 1 chosen card, Conjures a Spell.
        await ResetHandAndEnergy();
        var altar = combatState.CreateCard<Altar>(player);
        var fodder = combatState.CreateCard<AwakenedStrike>(player);
        await CardPileCmd.Add(altar, PileType.Hand);
        await CardPileCmd.Add(fodder, PileType.Hand);
        int conjuresBefore = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, fodder)))
        {
            AutoTestAssert.True(altar.TryManualPlay(null), "Play Altar");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(5, player.Creature.Block, "Altar gained 5 Block");
        AutoTestAssert.Equal(PileType.Exhaust, fodder.Pile?.Type, "Altar exhausted chosen card");
        AutoTestAssert.Equal(conjuresBefore + 1, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "Altar Conjured a Spell");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<AwakenedSpellCard>().Any(), "Altar added conjured Spell to Hand");

        // Altar sole-card-in-hand edge case: still gains Block and Conjures cleanly with nothing to Exhaust.
        await ResetHandAndEnergy();
        var altarSolo = combatState.CreateCard<Altar>(player);
        await CardPileCmd.Add(altarSolo, PileType.Hand);
        conjuresBefore = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        AutoTestAssert.True(altarSolo.TryManualPlay(null), "Play Altar as sole card in Hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.Block, "Altar solo gained 5 Block");
        AutoTestAssert.Equal(conjuresBefore + 1, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "Altar solo still Conjured");

        // 2. BirdsEye (base + upgraded): choose a specific remaining Spell; upgraded refreshes Spellbook first!
        await ResetHandAndEnergy();
        var targetCanonical = AutoTestAssert.NotNull(
            AwakenedSpellbook.GetRemainingQueue(combatState, player).FirstOrDefault(),
            "Remaining Spell in queue");
        int queueBefore = AwakenedSpellbook.GetRemainingQueueCount(combatState, player);
        var birdsEye = combatState.CreateCard<BirdsEye>(player);
        await CardPileCmd.Add(birdsEye, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c.CanonicalInstance, targetCanonical)))
        {
            AutoTestAssert.True(birdsEye.TryManualPlay(null), "Play BirdsEye");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(
            pcs.Hand.Cards.Any(c => ReferenceEquals(c.CanonicalInstance, targetCanonical)),
            "BirdsEye conjured the chosen Spell");
        if (queueBefore > 1)
        {
            AutoTestAssert.Equal(queueBefore - 1, AwakenedSpellbook.GetRemainingQueueCount(combatState, player), "BirdsEye removed chosen Spell from queue");
        }

        // BirdsEye+: refreshes Spellbook back to full size (4) before choosing, leaving 3 in queue.
        await ResetHandAndEnergy();
        var birdsEyePlus = combatState.CreateCard<BirdsEye>(player);
        CardCmd.Upgrade(birdsEyePlus);
        await CardPileCmd.Add(birdsEyePlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is Thunderbolt))
        {
            AutoTestAssert.True(birdsEyePlus.TryManualPlay(null), "Play BirdsEye+");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(pcs.Hand.Cards.OfType<Thunderbolt>().Any(), "BirdsEye+ conjured chosen Thunderbolt after refresh");
        AutoTestAssert.Equal(3, AwakenedSpellbook.GetRemainingQueueCount(combatState, player), "BirdsEye+ refreshed Spellbook (4 -> 3 after choosing 1)");

        // 3. BloodRite: deals 8 damage and adds Ceremony to Hand.
        await ResetHandAndEnergy();
        var bloodRite = combatState.CreateCard<BloodRite>(player);
        await CardPileCmd.Add(bloodRite, PileType.Hand);
        int hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(bloodRite.TryManualPlay(enemy1), "Play BloodRite");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 8, enemy1.CurrentHp, "BloodRite dealt 8 damage");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<Ceremony>().Any(), "BloodRite added Ceremony to Hand");

        // 4. Carrionmaker: 1 hit (9 dmg) with 0 Spells played this turn, then 3 hits (27 dmg) after 2 Spells!
        await ResetHandAndEnergy();
        var carrion1 = combatState.CreateCard<Carrionmaker>(player);
        await CardPileCmd.Add(carrion1, PileType.Hand);
        int totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(carrion1.TryManualPlay(null), "Play Carrionmaker with 0 Spells played");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 9, enemy1.CurrentHp + enemy2.CurrentHp, "Carrionmaker dealt 1x9=9 damage with 0 Spells played");

        // Play 2 Spells (Cryostasis + EspSpell) this turn, then play Carrionmaker again -> 3x9 = 27 damage!
        var spellA = combatState.CreateCard<Cryostasis>(player);
        var spellB = combatState.CreateCard<EspSpell>(player);
        await CardPileCmd.Add(spellA, PileType.Hand);
        await CardPileCmd.Add(spellB, PileType.Hand);
        AutoTestAssert.True(spellA.TryManualPlay(null), "Play Spell 1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(spellB.TryManualPlay(null), "Play Spell 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        await ResetHandAndEnergy();
        var carrion2 = combatState.CreateCard<Carrionmaker>(player);
        await CardPileCmd.Add(carrion2, PileType.Hand);
        totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(carrion2.TryManualPlay(null), "Play Carrionmaker after 2 Spells");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 27, enemy1.CurrentHp + enemy2.CurrentHp, "Carrionmaker dealt 3x9=27 damage after 2 Spells played");

        // 5. Caw: without Chant does NOT buff; with Chant buffs ALL Caw copies in combat by +4!
        await ResetHandAndEnergy();
        var cawNoChant = combatState.CreateCard<Caw>(player);
        var cawAlly = combatState.CreateCard<Caw>(player);
        await CardPileCmd.Add(cawNoChant, PileType.Hand);
        await CardPileCmd.Add(cawAlly, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(cawNoChant.TryManualPlay(enemy1), "Play Caw without Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 4, enemy1.CurrentHp, "Caw without Chant dealt 4 damage");
        AutoTestAssert.Equal(4m, cawAlly.DynamicVars.Damage.BaseValue, "Caw without Chant did NOT buff other Caw");

        // Play a Power (Ceremony), then play Caw -> triggers Chant and buffs all Caws by +4!
        var ceremony = combatState.CreateCard<Ceremony>(player);
        await CardPileCmd.Add(ceremony, PileType.Hand);
        AutoTestAssert.True(ceremony.TryManualPlay(null), "Play Ceremony (Power) to activate Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var cawChant = combatState.CreateCard<Caw>(player);
        await CardPileCmd.Add(cawChant, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(cawChant.TryManualPlay(enemy1), "Play Caw with Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 5, enemy1.CurrentHp, "Caw dealt 4 + 1 Strength (from Ceremony) = 5 damage");
        AutoTestAssert.Equal(8m, cawChant.DynamicVars.Damage.BaseValue, "Caw Chant increased played Caw copy base damage (4 -> 8)");
        AutoTestAssert.Equal(8m, cawAlly.DynamicVars.Damage.BaseValue, "Caw Chant increased all Caw base damage by +4 (4 -> 8)");
        AutoTestAssert.Equal(8m, cawNoChant.DynamicVars.Damage.BaseValue, "Caw Chant also increased Caw in Discard (4 -> 8)");

        // Clear Ceremony Strength for clean damage checks ahead.
        if (player.Creature.GetPower<StrengthPower>() is { } strP)
            await PowerCmd.Remove(strP);

        // 6. ChosenVerse: does NOT self-trigger, ignores Attacks, triggers on non-Attacks, supports stacking!
        await ResetHandAndEnergy();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedStrike>(player), PileType.Draw);
        var chosenVerse1 = combatState.CreateCard<ChosenVerse>(player);
        await CardPileCmd.Add(chosenVerse1, PileType.Hand);
        int handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(chosenVerse1.TryManualPlay(null), "Play ChosenVerse");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.Block, "ChosenVerse did NOT trigger on itself");
        AutoTestAssert.Equal(handBefore - 1, pcs.Hand.Cards.Count, "ChosenVerse did NOT draw on itself");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<ChosensVersePower>(), "ChosenVerse applied 2 triggers");

        // Attack should NOT trigger ChosenVerse.
        var atk = combatState.CreateCard<AwakenedStrike>(player);
        await CardPileCmd.Add(atk, PileType.Hand);
        AutoTestAssert.True(atk.TryManualPlay(enemy1), "Play Attack while ChosenVerse active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.Block, "Attack did NOT trigger ChosenVerse");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<ChosensVersePower>(), "Attack kept ChosenVerse at 2");

        // Skill triggers ChosenVerse: draws 1 card and gains 4 Block, decrements to 1.
        var defend = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(defend, PileType.Hand);
        int handBeforeSkill = pcs.Hand.Cards.Count;
        AutoTestAssert.True(defend.TryManualPlay(null), "Play Defend (non-Attack) to trigger ChosenVerse");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5 + 4, player.Creature.Block, "Defend (5) + ChosenVerse (4) = 9 Block");
        AutoTestAssert.Equal(handBeforeSkill, pcs.Hand.Cards.Count, "ChosenVerse drew 1 card (-1 played +1 drawn)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ChosensVersePower>(), "ChosenVerse decremented to 1");

        // Play ChosenVerse+ (6 Block) while 1st ChosenVerse still has 2 triggers left:
        // Entry 1 (4 Block) fires on ChosenVerse+ (leaving 1), Entry 2 (6 Block) starts at 2 -> both entries overlap!
        await ResetHandAndEnergy();
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedStrike>(player), PileType.Draw);
        if (player.Creature.GetPower<ChosensVersePower>() is { } oldCv)
            await PowerCmd.Remove(oldCv);
        var cvOverlap1 = combatState.CreateCard<ChosenVerse>(player);
        var cvOverlap2 = combatState.CreateCard<ChosenVerse>(player);
        CardCmd.Upgrade(cvOverlap2);
        var overlapDefend = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(cvOverlap1, PileType.Hand);
        await CardPileCmd.Add(cvOverlap2, PileType.Hand);
        await CardPileCmd.Add(overlapDefend, PileType.Hand);
        AutoTestAssert.True(cvOverlap1.TryManualPlay(null), "Play ChosenVerse (overlap 1)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(cvOverlap2.TryManualPlay(null), "Play ChosenVerse+ (overlap 2)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "Entry 1 triggered on ChosenVerse+ (+4 Block), Entry 2 did not self-trigger");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ChosensVersePower>(), "Total remaining triggers = 1 (Entry 1) + 2 (Entry 2) = 3");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int handBeforeOverlap = pcs.Hand.Cards.Count;
        AutoTestAssert.True(overlapDefend.TryManualPlay(null), "Play Defend while BOTH entries (4 and 6 Block) are alive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5 + 4 + 6, player.Creature.Block, "Simultaneous ChosenVerse entries granted 5 + 4 + 6 = 15 Block");
        AutoTestAssert.Equal(handBeforeOverlap + 1, pcs.Hand.Cards.Count, "Simultaneous ChosenVerse entries drew 2 cards (-1 played +2 drawn)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ChosensVersePower>(), "Simultaneous trigger decremented by 2 (3 -> 1)");

        // End of turn removes ChosensVersePower.
        var cvPower = AutoTestAssert.NotNull(player.Creature.GetPower<ChosensVersePower>(), "ChosensVersePower active");
        await cvPower.AfterSideTurnEnd(ctx, CombatSide.Player, [player.Creature]);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ChosensVersePower>(), "ChosensVersePower removed at turn end");

        // 7. Ensorcelate + DarkEcho + DarkEchoPower:
        // Ensorcelate gains 10 Block and makes next Power (DarkEcho, base cost 2) cost 0!
        await ResetHandAndEnergy(energy: 6);
        var ensorcelate = combatState.CreateCard<Ensorcelate>(player);
        var darkEcho1 = combatState.CreateCard<DarkEcho>(player);
        var darkEcho2 = combatState.CreateCard<DarkEcho>(player);
        await CardPileCmd.Add(ensorcelate, PileType.Hand);
        await CardPileCmd.Add(darkEcho1, PileType.Hand);
        await CardPileCmd.Add(darkEcho2, PileType.Hand);
        AutoTestAssert.True(ensorcelate.TryManualPlay(null), "Play Ensorcelate (2 Energy)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(10, player.Creature.Block, "Ensorcelate gained 10 Block");
        AutoTestAssert.Equal(4, pcs.Energy, "Ensorcelate cost 2 Energy (6 -> 4)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnsorcelatePower>(), "Ensorcelate applied 1 EnsorcelatePower");
        AutoTestAssert.Equal(0, darkEcho1.EnergyCost.GetAmountToSpend(), "DarkEcho costs 0 under EnsorcelatePower");

        // Non-Power play and AutoPlay of a Power must NOT consume EnsorcelatePower!
        var nonPowerSkill = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(nonPowerSkill, PileType.Hand);
        AutoTestAssert.True(nonPowerSkill.TryManualPlay(null), "Play Skill while EnsorcelatePower active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnsorcelatePower>(), "Non-Power did NOT consume EnsorcelatePower");
        var autoPower = combatState.CreateCard<Ceremony>(player);
        await CardCmd.AutoPlay(ctx, autoPower, null);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnsorcelatePower>(), "AutoPlay Power did NOT consume EnsorcelatePower");
        if (player.Creature.GetPower<StrengthPower>() is { } strAuto)
            await PowerCmd.Remove(strAuto);

        AutoTestAssert.True(darkEcho1.TryManualPlay(null), "Play DarkEcho for 0 Energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Energy, "DarkEcho spent 0 Energy under EnsorcelatePower (only Defend spent 1)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<EnsorcelatePower>(), "EnsorcelatePower consumed after Power play");
        AutoTestAssert.Equal(2, darkEcho2.EnergyCost.GetAmountToSpend(), "Second DarkEcho back to cost 2");

        // 2 stacks of EnsorcelatePower make 2 consecutive Powers cost 0!
        await PowerCmd.Apply<EnsorcelatePower>(ctx, player.Creature, 2m, player.Creature, null);
        var freeP1 = combatState.CreateCard<DarkEcho>(player);
        var freeP2 = combatState.CreateCard<DarknessFalls>(player);
        await CardPileCmd.Add(freeP1, PileType.Hand);
        await CardPileCmd.Add(freeP2, PileType.Hand);
        int energyBeforeTwoFree = pcs.Energy;
        AutoTestAssert.True(freeP1.TryManualPlay(null), "Play 1st free Power with 2 stacks");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnsorcelatePower>(), "1 stack remains after 1st Power");
        AutoTestAssert.True(freeP2.TryManualPlay(null), "Play 2nd free Power with 2 stacks");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeTwoFree, pcs.Energy, "Both Powers cost 0 Energy with 2 EnsorcelatePower stacks");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<EnsorcelatePower>(), "EnsorcelatePower depleted after 2 Powers");
        if (player.Creature.GetPower<DarknessFallsPower>() is { } tmpDfp) await PowerCmd.Remove(tmpDfp);
        if (player.Creature.GetPower<CursedStrengthPower>() is { } tmpCsp) await PowerCmd.Remove(tmpCsp);

        // DarkEchoPower is now stacked to 2 (darkEcho1 + freeP1).
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DarkEchoPower>(), "DarkEchoPower stacked to 2");

        // With +3 Strength + Vulnerable on enemy1, DarkEchoPower(2) still deals unpowered (4 + 3) * 2 = 14 to BOTH enemies!
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, 3m, player.Creature, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, enemy1, 2m, player.Creature, null);
        var darkEchoPower = AutoTestAssert.NotNull(player.Creature.GetPower<DarkEchoPower>(), "DarkEchoPower present");
        int e1Before = enemy1.CurrentHp;
        int e2Before = enemy2.CurrentHp;
        await darkEchoPower.BeforeSideTurnEnd(ctx, CombatSide.Player, [player.Creature]);
        AutoTestAssert.Equal(e1Before - 14, enemy1.CurrentHp, "DarkEchoPower(2) dealt unpowered 14 to Vulnerable enemy1 (not scaled by Vulnerable)");
        AutoTestAssert.Equal(e2Before - 14, enemy2.CurrentHp, "DarkEchoPower(2) with +3 Str dealt 14 to enemy2");

        // Negative Strength (-5 -> 4 - 5 = -1 clamped to 0) deals 0 damage!
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, -8m, player.Creature, null);
        e1Before = enemy1.CurrentHp;
        await darkEchoPower.BeforeSideTurnEnd(ctx, CombatSide.Player, [player.Creature]);
        AutoTestAssert.Equal(e1Before, enemy1.CurrentHp, "DarkEchoPower clamped negative total damage to 0");
        await PowerCmd.Remove(darkEchoPower);
        if (enemy1.GetPower<VulnerablePower>() is { } vulnP) await PowerCmd.Remove(vulnP);
        if (player.Creature.GetPower<StrengthPower>() is { } strP2)
            await PowerCmd.Remove(strP2);

        // 8. DarknessFalls + DarknessFallsPower + CursedStrengthPower:
        await ResetHandAndEnergy();
        var darknessFalls = combatState.CreateCard<DarknessFalls>(player);
        var darknessFallsPlus = combatState.CreateCard<DarknessFalls>(player);
        CardCmd.Upgrade(darknessFallsPlus);
        AutoTestAssert.True(darknessFallsPlus.Keywords.Contains(CardKeyword.Innate), "DarknessFalls+ is Innate");
        await CardPileCmd.Add(darknessFalls, PileType.Hand);
        AutoTestAssert.True(darknessFalls.TryManualPlay(null), "Play DarknessFalls");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<DarknessFallsPower>(), "DarknessFalls applied 4 DarknessFallsPower");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<CursedStrengthPower>(), "DarknessFalls applied 1 CursedStrengthPower");

        // Drain 1 Energy via Void draw -> gains 4 Block and 1 Strength!
        var voidCard = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(voidCard, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1m, player);
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "DarknessFallsPower gained 4 Block on Void Drain");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "CursedStrengthPower gained 1 Strength on Void Drain");

        // Remove DarknessFalls powers & Strength so remaining checks are isolated.
        if (player.Creature.GetPower<DarknessFallsPower>() is { } dfp) await PowerCmd.Remove(dfp);
        if (player.Creature.GetPower<CursedStrengthPower>() is { } csp) await PowerCmd.Remove(csp);
        if (player.Creature.GetPower<StrengthPower>() is { } strP3) await PowerCmd.Remove(strP3);

        // 9. DeathCoil: applies 8 Manaburn to enemy and 1 HermitDrainedPower to player.
        await ResetHandAndEnergy();
        var deathCoil = combatState.CreateCard<DeathCoil>(player);
        await CardPileCmd.Add(deathCoil, PileType.Hand);
        AutoTestAssert.True(deathCoil.TryManualPlay(enemy1), "Play DeathCoil");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, enemy1.GetPowerAmount<ManaburnPower>(), "DeathCoil applied 8 Manaburn");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HermitDrainedPower>(), "DeathCoil applied 1 Drained");
        if (enemy1.GetPower<ManaburnPower>() is { } mbp) await PowerCmd.Remove(mbp);
        if (player.Creature.GetPower<HermitDrainedPower>() is { } hdp) await PowerCmd.Remove(hdp);

        // 10. Eventide: deals 8x2 = 16 damage to random enemies and puts a Void on top of Draw Pile.
        await ResetHandAndEnergy();
        var eventide = combatState.CreateCard<Eventide>(player);
        await CardPileCmd.Add(eventide, PileType.Hand);
        totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(eventide.TryManualPlay(null), "Play Eventide");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 16, enemy1.CurrentHp + enemy2.CurrentHp, "Eventide dealt 8x2=16 damage");
        AutoTestAssert.True(PileType.Draw.GetPile(player).Cards.FirstOrDefault() is StsCards.Void, "Eventide put a Void on top of Draw Pile");

        // 11. Extension: deals 11 damage; stays in Discard on Skill, returns to Hand on Power!
        await ResetHandAndEnergy();
        var extension = combatState.CreateCard<Extension>(player);
        await CardPileCmd.Add(extension, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(extension.TryManualPlay(enemy1), "Play Extension");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 11, enemy1.CurrentHp, "Extension dealt 11 damage");
        AutoTestAssert.Equal(PileType.Discard, extension.Pile?.Type, "Extension went to Discard");

        var skillNonPower = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(skillNonPower, PileType.Hand);
        AutoTestAssert.True(skillNonPower.TryManualPlay(null), "Play Skill (non-Power)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(PileType.Discard, extension.Pile?.Type, "Extension stayed in Discard after Skill");

        var powerTrigger = combatState.CreateCard<Ceremony>(player);
        await CardPileCmd.Add(powerTrigger, PileType.Hand);
        AutoTestAssert.True(powerTrigger.TryManualPlay(null), "Play Ceremony (Power)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(PileType.Hand, extension.Pile?.Type, "Extension returned from Discard to Hand on Power play");

        // Extension full-hand guard: with 10 cards in Hand, playing a Power leaves Extension in Discard!
        await CardPileCmd.Add(extension, PileType.Discard);
        while (pcs.Hand.Cards.Count < CardPile.MaxCardsInHand)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedDefend>(player), PileType.Hand);
        var fullHandPower = combatState.CreateCard<Ceremony>(player);
        await CardCmd.AutoPlay(ctx, fullHandPower, null);
        AutoTestAssert.Equal(PileType.Discard, extension.Pile?.Type, "Extension stayed in Discard when Hand was full (10 cards)");
        if (player.Creature.GetPower<StrengthPower>() is { } strExt)
            await PowerCmd.Remove(strExt);

        // 12. FeatherVeil: 0-cost, gains 10 Block, loses 1 Strength (1 -> 0).
        await ResetHandAndEnergy();
        var featherVeil = combatState.CreateCard<FeatherVeil>(player);
        await CardPileCmd.Add(featherVeil, PileType.Hand);
        int strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(featherVeil.TryManualPlay(null), "Play FeatherVeil");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(10, player.Creature.Block, "FeatherVeil gained 10 Block");
        AutoTestAssert.Equal(strBefore - 1, player.Creature.GetPowerAmount<StrengthPower>(), "FeatherVeil reduced Strength by 1");

        // 13. FeatherWhirl (X-cost, base and upgraded):
        await ResetHandAndEnergy(energy: 2);
        var featherWhirl = combatState.CreateCard<FeatherWhirl>(player);
        await CardPileCmd.Add(featherWhirl, PileType.Hand);
        AutoTestAssert.True(featherWhirl.TryManualPlay(null), "Play FeatherWhirl at X=2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<PlumeJab>().Count(), "FeatherWhirl at X=2 added 2 Plume Jabs");

        await ResetHandAndEnergy(energy: 2);
        var featherWhirlPlus = combatState.CreateCard<FeatherWhirl>(player);
        CardCmd.Upgrade(featherWhirlPlus);
        await CardPileCmd.Add(featherWhirlPlus, PileType.Hand);
        AutoTestAssert.True(featherWhirlPlus.TryManualPlay(null), "Play FeatherWhirl+ at X=2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.OfType<PlumeJab>().Count(), "FeatherWhirl+ at X=2 added 3 Plume Jabs");

        // 14. Inscribe (base + upgraded): adds 2 copies of chosen Spell to Spellbook; upgraded also Conjures!
        await ResetHandAndEnergy();
        int qBeforeInscribe = AwakenedSpellbook.GetRemainingQueueCount(combatState, player);
        int cBeforeInscribe = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        var inscribe = combatState.CreateCard<Inscribe>(player);
        await CardPileCmd.Add(inscribe, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is BurningStudy))
        {
            AutoTestAssert.True(inscribe.TryManualPlay(null), "Play Inscribe");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(qBeforeInscribe + 2, AwakenedSpellbook.GetRemainingQueueCount(combatState, player), "Inscribe added 2 Spell copies to Spellbook queue");
        AutoTestAssert.Equal(cBeforeInscribe, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "Base Inscribe did NOT increment ConjuresThisCombat");

        await ResetHandAndEnergy();
        int cBeforeInscribePlus = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        var inscribePlus = combatState.CreateCard<Inscribe>(player);
        CardCmd.Upgrade(inscribePlus);
        await CardPileCmd.Add(inscribePlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is Darkleech))
        {
            AutoTestAssert.True(inscribePlus.TryManualPlay(null), "Play Inscribe+");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(cBeforeInscribePlus + 1, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "Inscribe+ Conjured 1 Spell");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<AwakenedSpellCard>().Any(), "Inscribe+ added Conjured Spell to Hand");
        AwakenedSpellbook.RefreshSpells(combatState, player);
        AutoTestAssert.Equal(4 + 4, AwakenedSpellbook.GetRemainingQueueCount(combatState, player), "Inscribed copies (2 + 2) persisted across RefreshSpells (total 8)");

        // 15. KnifesEdge: gains 2 Strength and adds 2 Voids to Discard Pile.
        await ResetHandAndEnergy();
        int voidsInDiscardBefore = PileType.Discard.GetPile(player).Cards.OfType<StsCards.Void>().Count();
        strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        var knifesEdge = combatState.CreateCard<KnifesEdge>(player);
        await CardPileCmd.Add(knifesEdge, PileType.Hand);
        AutoTestAssert.True(knifesEdge.TryManualPlay(null), "Play KnifesEdge");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(strBefore + 2, player.Creature.GetPowerAmount<StrengthPower>(), "KnifesEdge gained 2 Strength");
        AutoTestAssert.Equal(voidsInDiscardBefore + 2, PileType.Discard.GetPile(player).Cards.OfType<StsCards.Void>().Count(), "KnifesEdge added 2 Voids to Discard");

        // 16. MiddenHeap: Discard->Hand, Draw->Hand (no Void drain), MiddenHeap+ (2 cards), full-hand Draw->Discard overflow, and 0 candidates!
        await ResetHandAndEnergy(energy: 5);
        var midden = combatState.CreateCard<MiddenHeap>(player);
        await CardPileCmd.Add(midden, PileType.Hand);
        var discardVoid = PileType.Discard.GetPile(player).Cards.OfType<StsCards.Void>().First();
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, discardVoid)))
        {
            AutoTestAssert.True(midden.TryManualPlay(null), "Play MiddenHeap (Discard -> Hand)");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(3, player.Creature.Block, "MiddenHeap gained 3 Block");
        AutoTestAssert.Equal(PileType.Hand, discardVoid.Pile?.Type, "MiddenHeap moved Void from Discard to Hand");
        AutoTestAssert.Equal(5, pcs.Energy, "MiddenHeap moving Void to Hand did NOT drain Energy");

        // MiddenHeap+ moving 2 Voids (1 from Draw + 1 from Discard) without draining Energy!
        await ResetHandAndEnergy(energy: 5);
        var drawVoid = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(drawVoid, PileType.Draw, CardPilePosition.Top);
        var discardVoid2 = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(discardVoid2, PileType.Discard);
        var middenPlus = combatState.CreateCard<MiddenHeap>(player);
        CardCmd.Upgrade(middenPlus);
        await CardPileCmd.Add(middenPlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, drawVoid) || ReferenceEquals(c, discardVoid2)))
        {
            AutoTestAssert.True(middenPlus.TryManualPlay(null), "Play MiddenHeap+ (2 Status cards from Draw + Discard)");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(4, player.Creature.Block, "MiddenHeap+ gained 4 Block");
        AutoTestAssert.Equal(PileType.Hand, drawVoid.Pile?.Type, "MiddenHeap+ moved Void from Draw to Hand");
        AutoTestAssert.Equal(PileType.Hand, discardVoid2.Pile?.Type, "MiddenHeap+ moved 2nd Void from Discard to Hand");
        AutoTestAssert.Equal(5, pcs.Energy, "MiddenHeap+ moving Void from Draw did NOT drain Energy");

        // MiddenHeap+ with 9 other cards in Hand + 1 Void in Draw: 1st moves to Hand (filling to 10), 2nd from Draw overflows to Discard!
        await ResetHandAndEnergy(energy: 5);
        var overflowDrawVoid1 = combatState.CreateCard<StsCards.Void>(player);
        var overflowDrawVoid2 = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.Add(overflowDrawVoid1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(overflowDrawVoid2, PileType.Draw, CardPilePosition.Top);
        var middenFull = combatState.CreateCard<MiddenHeap>(player);
        CardCmd.Upgrade(middenFull);
        await CardPileCmd.Add(middenFull, PileType.Hand);
        while (pcs.Hand.Cards.Count < CardPile.MaxCardsInHand)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedDefend>(player), PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, overflowDrawVoid1) || ReferenceEquals(c, overflowDrawVoid2)))
        {
            AutoTestAssert.True(middenFull.TryManualPlay(null), "Play MiddenHeap+ with 9 other cards in Hand");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        int inHandOverflow = (overflowDrawVoid1.Pile?.Type == PileType.Hand ? 1 : 0) + (overflowDrawVoid2.Pile?.Type == PileType.Hand ? 1 : 0);
        int inDiscardOverflow = (overflowDrawVoid1.Pile?.Type == PileType.Discard ? 1 : 0) + (overflowDrawVoid2.Pile?.Type == PileType.Discard ? 1 : 0);
        AutoTestAssert.Equal(1, inHandOverflow, "MiddenHeap+ filled 10th Hand slot with 1st Draw Void");
        AutoTestAssert.Equal(1, inDiscardOverflow, "MiddenHeap+ overflowed 2nd Draw Void to Discard");

        // MiddenHeap with 0 Status/Curse candidates in Draw/Discard still grants Block cleanly!
        await ResetHandAndEnergy(energy: 5);
        foreach (var sc in PileType.Draw.GetPile(player).Cards.Concat(PileType.Discard.GetPile(player).Cards)
            .Where(c => c.Type is CardType.Status or CardType.Curse).ToList())
        {
            await CardCmd.Exhaust(ctx, sc);
        }
        var middenEmpty = combatState.CreateCard<MiddenHeap>(player);
        await CardPileCmd.Add(middenEmpty, PileType.Hand);
        AutoTestAssert.True(middenEmpty.TryManualPlay(null), "Play MiddenHeap with 0 Status/Curse candidates");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.Block, "MiddenHeap with 0 candidates still gained 3 Block");
    }

    public static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAwakenedOne>("DFAWK04");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 900m);
        await CreatureCmd.GainMaxHp(enemy2, 900m);

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

        // 1. MirePit: ALL enemies lose 6 Strength this turn (blocked by Artifact on enemy2), player gets 1 Drained, Exhausts!
        await ResetHandAndEnergy();
        await PowerCmd.Apply<ArtifactPower>(ctx, enemy2, 1m, enemy2, null);
        var mirePit = combatState.CreateCard<MirePit>(player);
        await CardPileCmd.Add(mirePit, PileType.Hand);
        AutoTestAssert.True(mirePit.TryManualPlay(null), "Play MirePit");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(-6, enemy1.GetPowerAmount<StrengthPower>(), "MirePit reduced enemy1 Strength by 6");
        AutoTestAssert.Equal(6, enemy1.GetPowerAmount<PiercingWailPower>(), "MirePit applied 6 temporary Strength loss to enemy1");
        AutoTestAssert.Equal(0, enemy2.GetPowerAmount<StrengthPower>(), "Artifact blocked MirePit on enemy2");
        AutoTestAssert.Equal(0, enemy2.GetPowerAmount<ArtifactPower>(), "Artifact consumed on enemy2");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HermitDrainedPower>(), "MirePit applied 1 Drained");
        AutoTestAssert.Equal(PileType.Exhaust, mirePit.Pile?.Type, "MirePit exhausted");
        if (player.Creature.GetPower<HermitDrainedPower>() is { } d1) await PowerCmd.Remove(d1);

        // 2. MoonlitVision: mid-turn clamp & stacking!
        // Play 2 Spells BEFORE MoonlitVision #1 -> #1 clamps _triggeredThisTurn=1 (0 triggers left this turn),
        // then MoonlitVision #2 stacks Amount=2 -> next Spell DOES refund +1 Energy!
        await ResetHandAndEnergy(energy: 10);
        var preSpell1 = combatState.CreateCard<Cryostasis>(player);
        var preSpell2 = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(preSpell1, PileType.Hand);
        await CardPileCmd.Add(preSpell2, PileType.Hand);
        AutoTestAssert.True(preSpell1.TryManualPlay(null), "Play pre-Spell 1 before MoonlitVision");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(preSpell2.TryManualPlay(null), "Play pre-Spell 2 before MoonlitVision");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        await ResetHandAndEnergy(energy: 8);
        var moonlit1 = combatState.CreateCard<MoonlitVision>(player);
        var moonlit2 = combatState.CreateCard<MoonlitVision>(player);
        await CardPileCmd.Add(moonlit1, PileType.Hand);
        await CardPileCmd.Add(moonlit2, PileType.Hand);
        AutoTestAssert.True(moonlit1.TryManualPlay(null), "Play MoonlitVision #1 after 2 Spells");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, pcs.Energy, "MoonlitVision #1 cost 2 (8 -> 6)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MoonlitVisionPower>(), "MoonlitVisionPower(1) applied");

        var mvSpellNoRefund = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(mvSpellNoRefund, PileType.Hand);
        AutoTestAssert.True(mvSpellNoRefund.TryManualPlay(null), "Play Spell after MoonlitVision #1 (already spent this turn)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, pcs.Energy, "MoonlitVision #1 gave 0 refund because Spell already played this turn (6 -> 5)");

        AutoTestAssert.True(moonlit2.TryManualPlay(null), "Play MoonlitVision #2 to stack to 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Energy, "MoonlitVision #2 cost 2 (5 -> 3)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<MoonlitVisionPower>(), "MoonlitVisionPower stacked to 2");

        var mvSpell1 = combatState.CreateCard<Cryostasis>(player);
        var mvSpell2 = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(mvSpell1, PileType.Hand);
        await CardPileCmd.Add(mvSpell2, PileType.Hand);
        AutoTestAssert.True(mvSpell1.TryManualPlay(null), "Play Spell after MoonlitVision #2 (cost 1 -> refunds 1)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Energy, "Stacked MoonlitVision #2 refunded +1 Energy (3 - 1 + 1 = 3)");
        AutoTestAssert.True(mvSpell2.TryManualPlay(null), "Play next Spell (both triggers now spent)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Energy, "MoonlitVision did NOT trigger again once exhausted (3 - 1 = 2)");

        // 3. MysticOrder: draws 2 cards and Conjures a Spell!
        await ResetHandAndEnergy();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedStrike>(player), PileType.Draw);
        var mystic = combatState.CreateCard<MysticOrder>(player);
        await CardPileCmd.Add(mystic, PileType.Hand);
        int conjuresBefore = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        AutoTestAssert.True(mystic.TryManualPlay(null), "Play MysticOrder");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "MysticOrder drew 2 cards + Conjured 1 Spell = 3 cards in Hand");
        AutoTestAssert.Equal(conjuresBefore + 1, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "MysticOrder Conjured 1 Spell");

        // 4. Primacy: 1st Strength gain draws 1 card; 2nd does NOT; negative Strength does NOT trigger!
        await ResetHandAndEnergy();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedStrike>(player), PileType.Draw);
        var primacy = combatState.CreateCard<Primacy>(player);
        await CardPileCmd.Add(primacy, PileType.Hand);
        AutoTestAssert.True(primacy.TryManualPlay(null), "Play Primacy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        // Negative Strength must NOT trigger or consume Primacy!
        var fvNeg = combatState.CreateCard<FeatherVeil>(player);
        await CardPileCmd.Add(fvNeg, PileType.Hand);
        AutoTestAssert.True(fvNeg.TryManualPlay(null), "Play FeatherVeil (-1 Strength)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count, "Negative Strength did NOT trigger Primacy");

        var cer1 = combatState.CreateCard<Ceremony>(player);
        var cer2 = combatState.CreateCard<Ceremony>(player);
        await CardPileCmd.Add(cer1, PileType.Hand);
        await CardPileCmd.Add(cer2, PileType.Hand);
        AutoTestAssert.True(cer1.TryManualPlay(null), "Play 1st Ceremony (+1 Strength)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Primacy drew 1 card on 1st Strength gain (cer2 + 1 drawn)");
        AutoTestAssert.True(cer2.TryManualPlay(null), "Play 2nd Ceremony (+1 Strength)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "Primacy did NOT draw on 2nd Strength gain");
        if (player.Creature.GetPower<StrengthPower>() is { } strClean1) await PowerCmd.Remove(strClean1);

        // 5. RavenStrike (Strike tag, Chant plays top card of Draw Pile; non-Chant does not):
        // Play a non-Power card first so last card played is NOT Ceremony (a Power)!
        await ResetHandAndEnergy();
        var clearChant = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(clearChant, PileType.Hand);
        AutoTestAssert.True(clearChant.TryManualPlay(null), "Play Defend so last card is non-Power");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        await ResetHandAndEnergy();
        var ravenNoChant = combatState.CreateCard<RavenStrike>(player);
        AutoTestAssert.True(ravenNoChant.Tags.Contains(CardTag.Strike), "RavenStrike has Strike tag");
        var topDefend = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(topDefend, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(ravenNoChant, PileType.Hand);
        int hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(ravenNoChant.TryManualPlay(enemy1), "Play RavenStrike without Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 15, enemy1.CurrentHp, "RavenStrike dealt 15 damage");
        AutoTestAssert.Equal(0, player.Creature.Block, "RavenStrike without Chant did NOT auto-play top card");
        AutoTestAssert.Equal(PileType.Draw, topDefend.Pile?.Type, "Top card stayed in Draw Pile");

        // Now play a Power (RisingChant — Ethereal base, non-Ethereal upgraded) to activate Chant AND RisingChantPower!
        var risingPlus = combatState.CreateCard<RisingChant>(player);
        AutoTestAssert.True(risingPlus.Keywords.Contains(CardKeyword.Ethereal), "Base RisingChant is Ethereal");
        CardCmd.Upgrade(risingPlus);
        AutoTestAssert.True(!risingPlus.Keywords.Contains(CardKeyword.Ethereal), "Upgraded RisingChant+ removes Ethereal");
        await CardPileCmd.Add(risingPlus, PileType.Hand);
        AutoTestAssert.True(risingPlus.TryManualPlay(null), "Play RisingChant+ (Power -> activates Chant + RisingChantPower)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<RisingChantPower>(), "RisingChantPower(1) active");

        // 6. Victuals with Chant + RisingChantPower(1): doubles +2 Energy -> +4 Energy, Exhausts!
        await ResetHandAndEnergy(energy: 2);
        var victuals = combatState.CreateCard<Victuals>(player);
        await CardPileCmd.Add(victuals, PileType.Hand);
        AutoTestAssert.True(victuals.TryManualPlay(null), "Play Victuals with Chant + RisingChantPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, pcs.Energy, "Victuals + RisingChantPower gained 2x2 = +4 Energy (2 -> 6)");
        AutoTestAssert.Equal(PileType.Exhaust, victuals.Pile?.Type, "Victuals exhausted");

        // 2nd Chant in the same turn (RavenStrike after Ceremony) only triggers ONCE (RisingChantPower spent for this turn)!
        await ResetHandAndEnergy();
        var topDefend2 = combatState.CreateCard<AwakenedDefend>(player);
        var secondDefend = combatState.CreateCard<AwakenedDefend>(player);
        await CardPileCmd.Add(secondDefend, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(topDefend2, PileType.Draw, CardPilePosition.Top);
        var cerForRaven = combatState.CreateCard<Ceremony>(player);
        var ravenChant = combatState.CreateCard<RavenStrike>(player);
        await CardPileCmd.Add(cerForRaven, PileType.Hand);
        await CardPileCmd.Add(ravenChant, PileType.Hand);
        AutoTestAssert.True(cerForRaven.TryManualPlay(null), "Play Ceremony for Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(ravenChant.TryManualPlay(enemy1), "Play RavenStrike with Chant (RisingChant already used)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.Block, "RavenStrike auto-played exactly 1 top card (Defend = 5 Block)");
        AutoTestAssert.Equal(PileType.Draw, secondDefend.Pile?.Type, "2nd card stayed in Draw Pile (RisingChant already spent)");
        if (player.Creature.GetPower<StrengthPower>() is { } strClean2) await PowerCmd.Remove(strClean2);

        // Victuals without Chant: gains 0 Energy, still Exhausts.
        await ResetHandAndEnergy(energy: 3);
        var victualsNoChant = combatState.CreateCard<Victuals>(player);
        await CardPileCmd.Add(victualsNoChant, PileType.Hand);
        AutoTestAssert.True(victualsNoChant.TryManualPlay(null), "Play Victuals without Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Energy, "Victuals without Chant gained 0 Energy");
        AutoTestAssert.Equal(PileType.Exhaust, victualsNoChant.Pile?.Type, "Victuals without Chant still exhausted");

        // 7. Siphon: Chant steals 2 Strength; Artifact blocks steal completely!
        await ResetHandAndEnergy();
        if (enemy1.GetPower<PiercingWailPower>() is { } pwClean) await PowerCmd.Remove(pwClean);
        if (enemy1.GetPower<StrengthPower>() is { } esClean) await PowerCmd.Remove(esClean);
        var cerForSiphon = combatState.CreateCard<Ceremony>(player);
        var siphon = combatState.CreateCard<Siphon>(player);
        await CardPileCmd.Add(cerForSiphon, PileType.Hand);
        await CardPileCmd.Add(siphon, PileType.Hand);
        AutoTestAssert.True(cerForSiphon.TryManualPlay(null), "Play Ceremony for Siphon Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int playerStrBeforeSiphon = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(siphon.TryManualPlay(enemy1), "Play Siphon with Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(-2, enemy1.GetPowerAmount<StrengthPower>(), "Siphon reduced enemy1 Strength by 2");
        AutoTestAssert.Equal(playerStrBeforeSiphon + 2, player.Creature.GetPowerAmount<StrengthPower>(), "Siphon gave player +2 temporary Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<FlexPotionPower>(), "Siphon applied FlexPotionPower(2)");

        // Siphon vs Artifact: enemy keeps Strength AND player does NOT gain Strength!
        await PowerCmd.Apply<ArtifactPower>(ctx, enemy2, 1m, enemy2, null);
        var cerForSiphon2 = combatState.CreateCard<Ceremony>(player);
        var siphonArtifact = combatState.CreateCard<Siphon>(player);
        await CardPileCmd.Add(cerForSiphon2, PileType.Hand);
        await CardPileCmd.Add(siphonArtifact, PileType.Hand);
        AutoTestAssert.True(cerForSiphon2.TryManualPlay(null), "Play Ceremony for Siphon vs Artifact");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int strBeforeArtifactSiphon = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(siphonArtifact.TryManualPlay(enemy2), "Play Siphon on Artifact enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, enemy2.GetPowerAmount<StrengthPower>(), "Artifact blocked Siphon Strength loss");
        AutoTestAssert.Equal(strBeforeArtifactSiphon, player.Creature.GetPowerAmount<StrengthPower>(), "Player did NOT gain Strength when Siphon was blocked by Artifact");
        if (player.Creature.GetPower<FlexPotionPower>() is { } flexClean) await PowerCmd.Remove(flexClean);
        if (player.Creature.GetPower<StrengthPower>() is { } strClean3) await PowerCmd.Remove(strClean3);

        // 8. SongOfSorrow: whenever a Void is created, ALL enemies lose 7 HP (ignores enemy Block)!
        await ResetHandAndEnergy();
        var song = combatState.CreateCard<SongOfSorrow>(player);
        await CardPileCmd.Add(song, PileType.Hand);
        AutoTestAssert.True(song.TryManualPlay(null), "Play SongOfSorrow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        await CreatureCmd.GainBlock(enemy1, 50m, ValueProp.Unpowered, null);
        int e1Before = enemy1.CurrentHp;
        int e2Before = enemy2.CurrentHp;
        var knifes = combatState.CreateCard<KnifesEdge>(player);
        await CardPileCmd.Add(knifes, PileType.Hand);
        AutoTestAssert.True(knifes.TryManualPlay(null), "Play KnifesEdge (creates 2 Voids)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 14, enemy1.CurrentHp, "SongOfSorrow dealt 2x7 = 14 unblockable HP loss to blocked enemy1");
        AutoTestAssert.Equal(e2Before - 14, enemy2.CurrentHp, "SongOfSorrow dealt 2x7 = 14 unblockable HP loss to enemy2");
        // Enemy-added Void (creator == null) must NOT trigger SongOfSorrow!
        e1Before = enemy1.CurrentHp;
        var enemyVoid = combatState.CreateCard<StsCards.Void>(player);
        await CardPileCmd.AddGeneratedCardToCombat(enemyVoid, PileType.Discard, creator: null);
        AutoTestAssert.Equal(e1Before, enemy1.CurrentHp, "Enemy-created Void (creator == null) did NOT trigger SongOfSorrow");
        await CreatureCmd.LoseBlock(ctx, enemy1, enemy1.Block, null);
        if (player.Creature.GetPower<StrengthPower>() is { } strClean4) await PowerCmd.Remove(strClean4);

        // 9. SoulStrike: Strike tag, 4x3 = 12 damage, costs 0 right now because >= 3 Powers were played this turn!
        await ResetHandAndEnergy(energy: 5);
        var soulStrike = combatState.CreateCard<SoulStrike>(player);
        AutoTestAssert.True(soulStrike.Tags.Contains(CardTag.Strike), "SoulStrike has Strike tag");
        await CardPileCmd.Add(soulStrike, PileType.Hand);
        AutoTestAssert.Equal(0, soulStrike.EnergyCost.GetAmountToSpend(), "SoulStrike costs 0 after 3+ Powers played this turn");
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(soulStrike.TryManualPlay(enemy1), "Play SoulStrike for 0 Energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, pcs.Energy, "SoulStrike spent 0 Energy");
        AutoTestAssert.Equal(hpBefore - 12, enemy1.CurrentHp, "SoulStrike dealt 4x3 = 12 damage");

        // 10. SplitWide + StrengthReturnPower: deals 5, exhausts, does NOT self-trigger; next attack grants +1 temp Strength!
        await ResetHandAndEnergy();
        var splitWide = combatState.CreateCard<SplitWide>(player);
        await CardPileCmd.Add(splitWide, PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(splitWide.TryManualPlay(enemy1), "Play SplitWide");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 5, enemy1.CurrentHp, "SplitWide dealt 5 damage");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<StrengthReturnPower>(), "SplitWide applied StrengthReturnPower(1)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "SplitWide did NOT self-trigger StrengthReturnPower");
        AutoTestAssert.Equal(PileType.Exhaust, splitWide.Pile?.Type, "SplitWide exhausted");

        // Unpowered damage must NOT trigger StrengthReturnPower!
        await CreatureCmd.Damage(ctx, enemy1, 3m, ValueProp.Unpowered, dealer: player.Creature, cardSource: null, cardPlay: null);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "Unpowered damage did NOT trigger StrengthReturnPower");

        var followUpAtk = combatState.CreateCard<AwakenedStrike>(player);
        await CardPileCmd.Add(followUpAtk, PileType.Hand);
        AutoTestAssert.True(followUpAtk.TryManualPlay(enemy1), "Attack enemy1 with StrengthReturnPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Attacking SplitWide enemy granted +1 temporary Strength");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FlexPotionPower>(), "SplitWide granted FlexPotionPower(1)");
        if (player.Creature.GetPower<FlexPotionPower>() is { } flexClean2) await PowerCmd.Remove(flexClean2);
        if (player.Creature.GetPower<StrengthPower>() is { } strClean5) await PowerCmd.Remove(strClean5);

        // 11. StormRuler + GrimoirePower: Conjures and adds +6 damage ONLY to Thunderbolts!
        await ResetHandAndEnergy();
        conjuresBefore = AwakenedSpellbook.GetConjuresThisCombat(combatState, player);
        var stormRuler = combatState.CreateCard<StormRuler>(player);
        await CardPileCmd.Add(stormRuler, PileType.Hand);
        AutoTestAssert.True(stormRuler.TryManualPlay(null), "Play StormRuler");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(conjuresBefore + 1, AwakenedSpellbook.GetConjuresThisCombat(combatState, player), "StormRuler Conjured");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<GrimoirePower>(), "StormRuler applied GrimoirePower(6)");

        var bolt = combatState.CreateCard<Thunderbolt>(player);
        await CardPileCmd.Add(bolt, PileType.Hand);
        hpBefore = enemy2.CurrentHp;
        AutoTestAssert.True(bolt.TryManualPlay(enemy2), "Play Thunderbolt under GrimoirePower(6)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 18, enemy2.CurrentHp, "Thunderbolt dealt 12 + 6 = 18 damage under GrimoirePower(6)");

        var plainStrike = combatState.CreateCard<AwakenedStrike>(player);
        await CardPileCmd.Add(plainStrike, PileType.Hand);
        hpBefore = enemy2.CurrentHp;
        AutoTestAssert.True(plainStrike.TryManualPlay(enemy2), "Play Strike under GrimoirePower(6)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy2.CurrentHp, "GrimoirePower did NOT boost non-Thunderbolt Strike");

        // 12. WaveOfMiasma: gains 12 Block, applies 4 Manaburn to ALL enemies, Exhausts!
        await ResetHandAndEnergy();
        var wave = combatState.CreateCard<WaveOfMiasma>(player);
        await CardPileCmd.Add(wave, PileType.Hand);
        AutoTestAssert.True(wave.TryManualPlay(null), "Play WaveOfMiasma");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.Block, "WaveOfMiasma gained 12 Block");
        AutoTestAssert.Equal(4, enemy1.GetPowerAmount<ManaburnPower>(), "WaveOfMiasma applied 4 Manaburn to enemy1");
        AutoTestAssert.Equal(4, enemy2.GetPowerAmount<ManaburnPower>(), "WaveOfMiasma applied 4 Manaburn to enemy2");
        AutoTestAssert.Equal(PileType.Exhaust, wave.Pile?.Type, "WaveOfMiasma exhausted");

        // 13. Cross-turn verification: TakeFlight (without & with Chant -> Blur), Thaumaturgy, Spellshield (real turn-end AfterFlush!), SingularityShield (real BlockNextTurnPower!)
        // First test TakeFlight WITHOUT Chant (last card was WaveOfMiasma, a Skill) -> no BlurPower!
        await ResetHandAndEnergy();
        var takeFlightNoChant = combatState.CreateCard<TakeFlight>(player);
        await CardPileCmd.Add(takeFlightNoChant, PileType.Hand);
        AutoTestAssert.True(takeFlightNoChant.TryManualPlay(null), "Play TakeFlight without Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.Block, "TakeFlight without Chant gained 12 Block");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<BlurPower>(), "TakeFlight without Chant did NOT apply BlurPower");

        await ResetHandAndEnergy();
        var thaum = combatState.CreateCard<Thaumaturgy>(player);
        await CardPileCmd.Add(thaum, PileType.Hand);
        AutoTestAssert.True(thaum.TryManualPlay(null), "Play Thaumaturgy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DexterityPower>(), "Thaumaturgy granted +1 Dexterity");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<ThaumaturgyPower>(), "Thaumaturgy applied ThaumaturgyPower(2)");

        // Spellshield (2 Block per Retained card)
        var spellshield = combatState.CreateCard<Spellshield>(player);
        await CardPileCmd.Add(spellshield, PileType.Hand);
        AutoTestAssert.True(spellshield.TryManualPlay(null), "Play Spellshield");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<SpellshieldPower>(), "Spellshield applied SpellshieldPower(2)");

        // TakeFlight right after Spellshield (Power -> Chant active!): gains 12 + 1 Dex = 13 Block + BlurPower(1)!
        var takeFlight = combatState.CreateCard<TakeFlight>(player);
        await CardPileCmd.Add(takeFlight, PileType.Hand);
        AutoTestAssert.True(takeFlight.TryManualPlay(null), "Play TakeFlight with Chant");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(13, player.Creature.Block, "TakeFlight gained 12 + 1 Dex = 13 Block");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<BlurPower>(), "TakeFlight Chant applied BlurPower(1)");

        // SingularityShield: gains 8 + 1 Dex = 9 Block now, applies BlockNextTurnPower(9) and HermitDrainedPower(1)!
        var singShield = combatState.CreateCard<SingularityShield>(player);
        await CardPileCmd.Add(singShield, PileType.Hand);
        AutoTestAssert.True(singShield.TryManualPlay(null), "Play SingularityShield");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(22, player.Creature.Block, "SingularityShield added 9 Block (13 -> 22)");
        AutoTestAssert.Equal(9, player.Creature.GetPowerAmount<BlockNextTurnPower>(), "SingularityShield applied BlockNextTurnPower(9)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HermitDrainedPower>(), "SingularityShield applied 1 Drained");

        // Hold 2 Retained Spells in Hand (BurningStudy + Cryostasis) and drop enemy Strength so enemy attacks deal 0.
        // Real end-of-turn flush triggers SpellshieldPower (+4 Block), Blur preserves 22 + 4 = 26, and BlockNextTurnPower adds +9 -> exact 35 Block on Round 2!
        var retain1 = combatState.CreateCard<BurningStudy>(player);
        var retain2 = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(retain1, PileType.Hand);
        await CardPileCmd.Add(retain2, PileType.Hand);
        await PowerCmd.Apply<StrengthPower>(ctx, enemy1, -50m, enemy1, null);
        await PowerCmd.Apply<StrengthPower>(ctx, enemy2, -50m, enemy2, null);

        // Advance to Round 2: verify REAL cross-turn Spellshield + Blur + BlockNextTurnPower + Thaumaturgy + Drained + Primacy/RisingChant/MoonlitVision resets!
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(22 + 4 + 9, player.Creature.Block, "Real turn-end Spellshield (+4) + Blur (kept 26) + SingularityShield BlockNextTurn (+9) = 35 Block");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<Ceremony>().Any(), "ThaumaturgyPower added Ceremony to Hand on Turn 2 start");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ThaumaturgyPower>(), "ThaumaturgyPower decremented 2 -> 1");
        AutoTestAssert.Equal(player.MaxEnergy - 1, pcs.Energy, "SingularityShield Drained reduced Turn 2 Energy by 1");

        // On Turn 2 (0 Powers played yet), SoulStrike is back to full cost 3!
        var soulStrikeT2 = combatState.CreateCard<SoulStrike>(player);
        await CardPileCmd.Add(soulStrikeT2, PileType.Hand);
        AutoTestAssert.Equal(3, soulStrikeT2.EnergyCost.GetAmountToSpend(), "SoulStrike reset to full cost 3 on Turn 2");

        // Play the Ceremony from Thaumaturgy: verifies PrimacyPower reset (+1 draw), SoulStrike partial cost (3 -> 2), and activates Chant!
        for (int i = 0; i < 3; i++)
            await CardPileCmd.Add(combatState.CreateCard<AwakenedStrike>(player), PileType.Draw);
        var round2Ceremony = pcs.Hand.Cards.OfType<Ceremony>().First();
        int handBeforeR2Cer = pcs.Hand.Cards.Count;
        AutoTestAssert.True(round2Ceremony.TryManualPlay(null), "Play Round 2 Ceremony from Thaumaturgy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBeforeR2Cer, pcs.Hand.Cards.Count, "PrimacyPower reset on Turn 2 and drew 1 card (-1 played +1 drawn)");
        AutoTestAssert.Equal(2, soulStrikeT2.EnergyCost.GetAmountToSpend(), "SoulStrike reduced from 3 -> 2 after 1 Power on Turn 2");

        // Play Victuals right after Ceremony: verifies RisingChantPower reset on Turn 2 (2x2 = +4 Energy)!
        // Free hand space first (Turn 2 retained + drawn + Ceremony + RippedDoll + Primacy filled 10 slots).
        foreach (var c in pcs.Hand.Cards.Take(3).ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        int energyBeforeR2Victuals = pcs.Energy;
        var r2Victuals = combatState.CreateCard<Victuals>(player);
        await CardPileCmd.Add(r2Victuals, PileType.Hand);
        AutoTestAssert.Equal(PileType.Hand, r2Victuals.Pile?.Type, "r2Victuals is in Hand");
        AutoTestAssert.True(r2Victuals.TryManualPlay(null), "Play Victuals on Turn 2 with Chant + reset RisingChantPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeR2Victuals + 4, pcs.Energy, "RisingChantPower reset on Turn 2 and doubled Victuals (+4 Energy)");

        // MoonlitVision reset on Turn 2: 1st Spell refunds +1 Energy!
        int energyBeforeT2Spell = pcs.Energy;
        var t2Spell = combatState.CreateCard<Cryostasis>(player);
        await CardPileCmd.Add(t2Spell, PileType.Hand);
        AutoTestAssert.True(t2Spell.TryManualPlay(null), "Play 1st Spell on Turn 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeT2Spell, pcs.Energy, "MoonlitVision reset on Turn 2 and refunded 1 Energy");
    }
}
