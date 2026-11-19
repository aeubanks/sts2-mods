using Downfall.Awakened;
using Downfall.Awakened.Cards;
using Downfall.Awakened.Powers;
using Downfall.Awakened.Relics;
using Downfall.Hermit.Powers;
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
}
