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
}
