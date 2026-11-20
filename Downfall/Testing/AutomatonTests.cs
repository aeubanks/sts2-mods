using Downfall.Automaton;
using Downfall.Automaton.Cards;
using Downfall.Automaton.Orbs;
using Downfall.Automaton.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Testing;

internal static class AutomatonTests
{
    private static async Task PlayCardAsync(CardModel card, Creature? target = null)
    {
        AutoTestAssert.True(card.TryManualPlay(target), $"TryManualPlay({card.GetType().Name})");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
    }

    internal static async Task TestVisualsRuntime()
    {
        await DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheAutomaton>(
            "AUTO_VIS",
            path => path == "res://Downfall/images/automaton/char/automaton.png");
        DownfallAutoTestRunner.CleanupRun();

        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTO_ORBVIS");
        var ctx = new ThrowingPlayerChoiceContext();

        var orbManager = AutoTestAssert.NotNull(
            NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager,
            "Automaton live NOrbManager");

        AutoTestAssert.Equal(3, player.PlayerCombatState!.OrbQueue.Capacity, "Automaton starts combat with 3 orb slots");

        // Encode 1 card and verify EncodedCardOrb mounts cleanly in live NOrbManager with preloaded icon & label "1"!
        var rep = (Replicate)combatState.CreateCard<Replicate>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, rep);

        var orbs = AutomatonMechanics.GetEncodedOrbs(player);
        AutoTestAssert.Equal(1, orbs.Count, "1 EncodedCardOrb channeled");
        AutoTestAssert.Equal("1", orbs[0].PrimaryLabelText, "Slot 0 EncodedCardOrb label is '1'");

        var orbNodes = new List<NOrb>();
        DownfallAutoTestRunner.CollectNodes(orbManager, orbNodes);
        var orbNode = AutoTestAssert.NotNull(
            orbNodes.FirstOrDefault(n => ReferenceEquals(n.Model, orbs[0])),
            "Live NOrb mounted for EncodedCardOrb");

        var sprites = new List<Sprite2D>();
        DownfallAutoTestRunner.CollectNodes(orbNode, sprites);
        var sprite = AutoTestAssert.NotNull(
            sprites.FirstOrDefault(s => s.Texture is not null),
            "EncodedCardOrb Sprite2D has loaded texture");
        AutoTestAssert.Equal(
            "res://Downfall/images/automaton/orbs/encoded_card_orb.png",
            sprite.Texture!.ResourcePath,
            "EncodedCardOrb uses preloaded bronze sequence orb icon");

        // Verify Skill, Attack, and Power FunctionCards all add to Hand and mount NCard visuals cleanly!
        var skillFunc = (FunctionCard)combatState.CreateCard<FunctionCard>(player);
        skillFunc.InitializeFromEncodedCards([(BronzeCard)combatState.CreateCard<Goto>(player)]);
        AutoTestAssert.Equal(CardType.Skill, skillFunc.Type, "Goto-only FunctionCard has CardType.Skill");
        await CardPileCmd.AddGeneratedCardToCombat(skillFunc, PileType.Hand, player);

        var atkFunc = (FunctionCard)combatState.CreateCard<FunctionCard>(player);
        atkFunc.InitializeFromEncodedCards([(BronzeCard)combatState.CreateCard<Replicate>(player)]);
        AutoTestAssert.Equal(CardType.Attack, atkFunc.Type, "Replicate FunctionCard has CardType.Attack");
        await CardPileCmd.AddGeneratedCardToCombat(atkFunc, PileType.Hand, player);

        var pwrFunc = (FunctionCard)combatState.CreateCard<FunctionCard>(player);
        pwrFunc.InitializeFromEncodedCards([(BronzeCard)combatState.CreateCard<Goto>(player)]);
        pwrFunc.SetFullRelease();
        AutoTestAssert.Equal(CardType.Power, pwrFunc.Type, "FullRelease FunctionCard has CardType.Power");
        AutoTestAssert.Equal(TargetType.Self, pwrFunc.TargetType, "FullRelease FunctionCard has TargetType.Self");
        await CardPileCmd.AddGeneratedCardToCombat(pwrFunc, PileType.Hand, player);
    }

    internal static async Task TestStarterEncodeAndFunctionCompile()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTO_START");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        // 1. Verify Character stats, starting deck, starter relic & Orobas upgrade
        AutoTestAssert.Equal(70, player.Creature.MaxHp, "Automaton MaxHp == 70");
        AutoTestAssert.Equal(99, player.Gold, "Automaton StartingGold == 99");
        AutoTestAssert.Equal(3, pcs.OrbQueue.Capacity, "Automaton starts with 3 sequence slots");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Automaton starts with 10 cards");
        var bronzeCore = AutoTestAssert.NotNull(player.GetRelic<BronzeCore>(), "Starts with BronzeCore");
        AutoTestAssert.Equal(RelicRarity.Starter, bronzeCore.Rarity, "BronzeCore is Starter");
        var orobas = AutoTestAssert.NotNull(bronzeCore.GetUpgradeReplacement(), "BronzeCore Orobas upgrade exists");
        AutoTestAssert.True(orobas is PlatinumCore, "BronzeCore upgrades into PlatinumCore");
        AutoTestAssert.Equal(RelicRarity.Starter, orobas.Rarity, "PlatinumCore has RelicRarity.Starter (excluded from normal pools)");

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 10;
        }

        // 2. Test Starter Strikes & Defends
        await ClearHandAndRefillAsync();
        int hpBeforeStrike = enemy.CurrentHp;
        var strike = combatState.CreateCard<BronzeStrike>(player);
        await CardPileCmd.Add(strike, PileType.Hand);
        await PlayCardAsync(strike, enemy);
        AutoTestAssert.Equal(hpBeforeStrike - 6, enemy.CurrentHp, "BronzeStrike dealt 6 damage");

        var defend = combatState.CreateCard<BronzeDefend>(player);
        await CardPileCmd.Add(defend, PileType.Hand);
        await PlayCardAsync(defend);
        AutoTestAssert.Equal(5, player.Creature.Block, "BronzeDefend granted 5 Block");

        // 3. Play 3 Encodable starter cards: Replicate -> Goto -> Replicate -> compiles copyingSprint()!
        await ClearHandAndRefillAsync();
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Draw, CardPilePosition.Top);
        int discardBefore = pcs.DiscardPile.Cards.Count;
        int hpBeforeRep1 = enemy.CurrentHp;

        var rep1 = combatState.CreateCard<Replicate>(player);
        await CardPileCmd.Add(rep1, PileType.Hand);
        await PlayCardAsync(rep1, enemy);
        AutoTestAssert.Equal(hpBeforeRep1 - 5, enemy.CurrentHp, "Replicate #1 dealt 5 damage");
        AutoTestAssert.Equal(1, AutomatonMechanics.GetEncodedOrbs(player).Count, "Replicate #1 encoded into slot 0");
        AutoTestAssert.True(!pcs.DiscardPile.Cards.Contains(rep1), "Original Replicate #1 is in sequence, not Discard");
        AutoTestAssert.Equal(discardBefore + 1, pcs.DiscardPile.Cards.Count, "Replicate #1 OnEncodedAsync added a copy to Discard");
        var discardRepCopy = AutoTestAssert.NotNull(
            pcs.DiscardPile.Cards.OfType<Replicate>().LastOrDefault(),
            "Replicate copy present in Discard");
        AutoTestAssert.Equal(-1, discardRepCopy.SequenceSlotIndex, "Replicate clone in Discard reset SequenceSlotIndex to -1");

        var goto1 = combatState.CreateCard<Goto>(player);
        await CardPileCmd.Add(goto1, PileType.Hand);
        int handBeforeGoto = pcs.Hand.Cards.Count;
        await PlayCardAsync(goto1);
        AutoTestAssert.Equal(handBeforeGoto, pcs.Hand.Cards.Count, "Goto drew 1 card (replacing itself)");
        AutoTestAssert.Equal(2, AutomatonMechanics.GetEncodedOrbs(player).Count, "Goto encoded into slot 1");

        // Prepare Energy to verify BronzeCore grants +1 Energy on 1st Function compiled!
        pcs.Energy = 3;
        var rep2 = combatState.CreateCard<Replicate>(player);
        await CardPileCmd.Add(rep2, PileType.Hand);
        await PlayCardAsync(rep2, enemy);

        AutoTestAssert.Equal(0, AutomatonMechanics.GetEncodedOrbs(player).Count, "Sequence cleared after 3rd card compiled Function");
        AutoTestAssert.Equal(1, AutomatonMechanics.GetFunctionsCompiledThisCombat(player), "FunctionsCompiledThisCombat == 1");
        AutoTestAssert.Equal(4, pcs.Energy, "BronzeCore granted +1 Energy on 1st Function compiled (3 -> 4)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "Goto OnCompileAsync granted DrawCardsNextTurnPower(1)");

        var func = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "Compiled FunctionCard added to Hand");
        AutoTestAssert.Equal("copyingSprint()", func.Title, "FunctionCard composed title 'copyingSprint()'");
        AutoTestAssert.Equal(CardType.Attack, func.Type, "FunctionCard with Replicate has CardType.Attack");
        AutoTestAssert.Equal(TargetType.AnyEnemy, func.TargetType, "FunctionCard with Replicate targets AnyEnemy");
        AutoTestAssert.Equal(1, func.EnergyCost.GetWithModifiers(CostModifiers.All), "FunctionCard costs 1 Energy");
        string desc = func.GetDescriptionForPile(PileType.Hand, enemy);
        AutoTestAssert.True(!desc.Contains('{') && !desc.Contains('}') && desc.Contains("damage") && desc.Contains("Draw"),
            $"FunctionCard description formatted cleanly: {desc}");

        // 4. Play compiled FunctionCard with +2 Strength -> deals (5+2) + (5+2) = 14 damage, draws 1 card, does NOT re-encode!
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, 2m, player.Creature, null);
        int hpBeforeFunc = enemy.CurrentHp;
        int handBeforeFunc = pcs.Hand.Cards.Count;
        await PlayCardAsync(func, enemy);
        AutoTestAssert.Equal(hpBeforeFunc - 14, enemy.CurrentHp, "FunctionCard dealt 2x(5+2)=14 damage with Strength");
        AutoTestAssert.Equal(handBeforeFunc, pcs.Hand.Cards.Count, "FunctionCard drew 1 card from Goto");
        AutoTestAssert.Equal(0, AutomatonMechanics.GetEncodedOrbs(player).Count, "Playing FunctionCard did not re-encode sub-cards");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 5. Verify FineTuning, DeepClone, PlayTwice, RemoveEncodedOrb re-indexing, and partial 1-card compile!
        var cloneFunc = (FunctionCard)func.CreateClone();
        cloneFunc.ApplyFineTune(2);
        string tunedBody = cloneFunc.BuildBodyText();
        AutoTestAssert.True(tunedBody.Contains("[blue]7[/blue] damage") && tunedBody.Contains("[blue]3[/blue] cards"),
            $"ApplyFineTune(2) updated FunctionCard sub-cards & dynamic Body: {tunedBody}");

        cloneFunc.SetPlayTwice();
        await CardPileCmd.Add(cloneFunc, PileType.Hand);
        int hpBeforeTwice = enemy.CurrentHp;
        await PlayCardAsync(cloneFunc, enemy);
        AutoTestAssert.Equal(hpBeforeTwice - 28, enemy.CurrentHp, "PlayTwice FunctionCard played twice (2 x 2 x 7 = 28 damage)");

        // Test RemoveEncodedOrb re-indexing + partial compile (and BronzeCore NOT triggering a 2nd time!)
        await ClearHandAndRefillAsync();
        pcs.Energy = 3;
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Goto>(player));
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Replicate>(player));
        var twoOrbs = AutomatonMechanics.GetEncodedOrbs(player);
        AutoTestAssert.Equal(2, twoOrbs.Count, "2 cards encoded");
        AutoTestAssert.True(AutomatonMechanics.RemoveEncodedOrb(player, twoOrbs[0]), "Removed slot 0 orb");
        var remainingOrbs = AutomatonMechanics.GetEncodedOrbs(player);
        AutoTestAssert.Equal(1, remainingOrbs.Count, "1 orb remaining after removal");
        AutoTestAssert.Equal(0, remainingOrbs[0].SequenceSlotIndex, "Remaining orb re-indexed to slot 0");
        AutoTestAssert.Equal("1", remainingOrbs[0].PrimaryLabelText, "Remaining orb label updated to '1'");

        var partialFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Partial 1-card compile succeeded");
        AutoTestAssert.Equal("Copy()", partialFunc.Title, "1-card Function uses single noun 'Copy()'");
        AutoTestAssert.Equal(3, pcs.Energy, "BronzeCore did NOT trigger a 2nd time in the same combat");

        // 6. Test PlatinumCore (Orobas upgrade): first 3 Functions cost 0 until played; skips 0-cost/Unplayable without wasting charge!
        await ClearHandAndRefillAsync();
        var plat = (PlatinumCore)await RelicCmd.Obtain<PlatinumCore>(player);
        AutoTestAssert.Equal(3, plat.DisplayAmount, "PlatinumCore AfterObtained initialized counter to 3 mid-combat");

        var freeFunc = (FunctionCard)combatState.CreateCard<FunctionCard>(player);
        freeFunc.EnergyCost.SetUntilPlayed(0);
        await plat.OnFunctionCompiledAsync(ctx, player, freeFunc);
        AutoTestAssert.Equal(3, plat.DisplayAmount, "PlatinumCore skipped already 0-cost Function without consuming charge");

        var unplayableFunc = (FunctionCard)combatState.CreateCard<FunctionCard>(player);
        unplayableFunc.AddKeyword(CardKeyword.Unplayable);
        await plat.OnFunctionCompiledAsync(ctx, player, unplayableFunc);
        AutoTestAssert.Equal(3, plat.DisplayAmount, "PlatinumCore skipped Unplayable Function without consuming charge");

        for (int i = 1; i <= 3; i++)
        {
            await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Replicate>(player));
            var pf = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), $"PlatinumCore Function #{i}");
            AutoTestAssert.Equal(0, pf.EnergyCost.GetWithModifiers(CostModifiers.All), $"PlatinumCore made Function #{i} cost 0");
            AutoTestAssert.Equal(3 - i, plat.DisplayAmount, $"PlatinumCore counter decremented to {3 - i}");
        }

        // 4th Function is NOT free (PlatinumCore out of charges)
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Replicate>(player));
        var pf4 = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "PlatinumCore Function #4");
        AutoTestAssert.Equal(1, pf4.EnergyCost.GetWithModifiers(CostModifiers.All), "4th Function costs 1 after PlatinumCore exhausted");

        // 7. Verify canonical FunctionCard instance remains untouched
        var canonicalFunc = ModelDb.Card<FunctionCard>();
        AutoTestAssert.Equal("function()", canonicalFunc.Title, "Canonical FunctionCard Title is 'function()'");
        AutoTestAssert.Equal(CardType.Skill, canonicalFunc.Type, "Canonical FunctionCard Type is Skill");
        AutoTestAssert.Equal(0, canonicalFunc.EncodedCards.Count, "Canonical FunctionCard has 0 EncodedCards");
    }
}
