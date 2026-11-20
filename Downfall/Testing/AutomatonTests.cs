using Downfall.Automaton;
using Downfall.Automaton.Cards;
using Downfall.Automaton.Orbs;
using Downfall.Automaton.Powers;
using Downfall.Automaton.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

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

    internal static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTO_COMMON");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1500);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        void ClearSequence()
        {
            foreach (var orb in AutomatonMechanics.GetEncodedOrbs(player))
                AutomatonMechanics.RemoveEncodedOrb(player, orb);
        }

        // 1. BitShift: unplayable when sequence empty; returns chosen Encoded card to Hand with Retain when non-empty!
        await ClearHandAndRefillAsync();
        ClearSequence();
        var bitShift = combatState.CreateCard<BitShift>(player);
        await CardPileCmd.Add(bitShift, PileType.Hand);
        AutoTestAssert.True(!bitShift.CanPlay(), "BitShift cannot be played when sequence is empty");

        var safeForShift = (Safeguard)combatState.CreateCard<Safeguard>(player);
        var depForShift = (Deprecate)combatState.CreateCard<Deprecate>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, safeForShift);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, depForShift);
        AutoTestAssert.True(bitShift.CanPlay(), "BitShift is playable when sequence has cards");
        using (DownfallAutoTestRunner.PreferCards(c => c is Deprecate))
        {
            await PlayCardAsync(bitShift);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(depForShift), "BitShift returned chosen Deprecate to Hand");
        AutoTestAssert.True(depForShift.Keywords.Contains(CardKeyword.Retain), "BitShift granted Retain to returned card");
        AutoTestAssert.Equal(-1, depForShift.SequenceSlotIndex, "Returned card SequenceSlotIndex reset to -1");
        AutoTestAssert.Equal(1, AutomatonMechanics.GetEncodedOrbs(player).Count, "1 orb left in sequence after BitShift");

        // 2. FineTuning + Safeguard end-to-end compile: increases numbers on Encoded cards by 1 (not cost) and survives Compile -> Play!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var fineTune = combatState.CreateCard<FineTuning>(player);
        await CardPileCmd.Add(fineTune, PileType.Hand);
        await PlayCardAsync(fineTune);
        AutoTestAssert.Equal(5, safeForShift.DynamicVars.Block.IntValue, "FineTuning increased encoded Safeguard Block (4 -> 5)");
        AutoTestAssert.Equal(0, safeForShift.EnergyCost.GetWithModifiers(CostModifiers.All), "FineTuning did not change cost");

        var tunedSafeFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled Fine-Tuned Safeguard into Function");
        await PlayCardAsync(tunedSafeFunc);
        AutoTestAssert.Equal(5, player.Creature.Block, "Playing Fine-Tuned Safeguard Function granted 5 Block (surviving CreateClone)");
        ClearSequence();

        // 3. Branch (both choices: BranchHit deals 7 & encodes BranchBlock; BranchBlock+ gains 8 Block & encodes BranchHit+!)
        await ClearHandAndRefillAsync();
        int hpBeforeBranch = enemy.CurrentHp;
        var branch1 = combatState.CreateCard<Branch>(player);
        await CardPileCmd.Add(branch1, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is BranchHit))
        {
            await PlayCardAsync(branch1, enemy);
        }
        AutoTestAssert.Equal(hpBeforeBranch - 7, enemy.CurrentHp, "Branch choosing Attack dealt 7 damage");
        var encodedAfterB1 = AutomatonMechanics.GetEncodedCards(player);
        AutoTestAssert.Equal(1, encodedAfterB1.Count, "Branch encoded unchosen option");
        AutoTestAssert.True(encodedAfterB1[0] is BranchBlock, "Branch choosing Attack encoded BranchBlock");

        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var branch2 = combatState.CreateCard<Branch>(player);
        CardCmd.Upgrade(branch2);
        await CardPileCmd.Add(branch2, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is BranchBlock))
        {
            await PlayCardAsync(branch2, enemy);
        }
        AutoTestAssert.Equal(8, player.Creature.Block, "Branch+ choosing Block gained 8 Block");
        var encodedAfterB2 = AutomatonMechanics.GetEncodedCards(player);
        AutoTestAssert.Equal(2, encodedAfterB2.Count, "Branch+ encoded 2nd option");
        AutoTestAssert.True(encodedAfterB2[1] is BranchHit { IsUpgraded: true }, "Branch+ choosing Block encoded upgraded BranchHit+");
        ClearSequence();

        // 4. BugBarrage: adds 2 Wounds to Hand, cycles all Statuses (1 pre-existing + 2 new = 3!), deals 5x3 = 15 dmg, draws 3!
        await ClearHandAndRefillAsync();
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<Dazed>(player), PileType.Hand);
        var bugBarrage = combatState.CreateCard<BugBarrage>(player);
        await CardPileCmd.Add(bugBarrage, PileType.Hand);
        int hpBeforeBug = enemy.CurrentHp;
        await PlayCardAsync(bugBarrage, enemy);
        AutoTestAssert.Equal(hpBeforeBug - 15, enemy.CurrentHp, "BugBarrage cycled 3 Statuses for 3 x 5 = 15 damage");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "BugBarrage drew 3 replacement cards after cycling 3 Statuses");

        // BugBarrage hand-full clamping: 9 non-Status cards + BugBarrage = 10 -> only 1 Wound fits in Hand (2nd overflows to Discard), deals 1x5 = 5 dmg & draws 1!
        await ClearHandAndRefillAsync();
        for (int i = 0; i < 9; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Hand);
        var bugFull = combatState.CreateCard<BugBarrage>(player);
        await CardPileCmd.Add(bugFull, PileType.Hand);
        int hpBeforeBugFull = enemy.CurrentHp;
        await PlayCardAsync(bugFull, enemy);
        AutoTestAssert.Equal(hpBeforeBugFull - 5, enemy.CurrentHp, "BugBarrage at full Hand only cycled the 1 Wound that fit in Hand (1 x 5 = 5 damage)");
        AutoTestAssert.Equal(10, pcs.Hand.Cards.Count, "BugBarrage at full Hand drew 1 replacement card (Hand stays at 10)");

        // 5. BuggyMess (Inserts Dazed, +1 Energy, Encodes) + CutThrough (5 dmg, Scry 2 discards top Dazed, Compile Draw 1) + Frontload (8 Block, Compile Retain) -> Function!
        await ClearHandAndRefillAsync();
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Draw, CardPilePosition.Top);
        int dazedBefore = pcs.DrawPile.Cards.Count(c => c is Dazed);
        pcs.Energy = 5;
        var buggy = combatState.CreateCard<BuggyMess>(player);
        await CardPileCmd.Add(buggy, PileType.Hand);
        await PlayCardAsync(buggy);
        AutoTestAssert.Equal(dazedBefore + 1, pcs.DrawPile.Cards.Count(c => c is Dazed), "BuggyMess inserted a Dazed into Draw Pile");
        AutoTestAssert.Equal(5, pcs.Energy, "BuggyMess cost 1 and gained +1 Energy (net 0)");

        var topScryDazed = combatState.CreateCard<Dazed>(player);
        await CardPileCmd.Add(topScryDazed, PileType.Draw, CardPilePosition.Top);
        int hpBeforeCut = enemy.CurrentHp;
        var cut = combatState.CreateCard<CutThrough>(player);
        await CardPileCmd.Add(cut, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topScryDazed)))
        {
            await PlayCardAsync(cut, enemy);
        }
        AutoTestAssert.Equal(hpBeforeCut - 5, enemy.CurrentHp, "CutThrough dealt 5 damage");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(topScryDazed) && !pcs.DrawPile.Cards.Contains(topScryDazed),
            "CutThrough Scry discarded chosen top Dazed from Draw Pile to Discard Pile");

        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var front = combatState.CreateCard<Frontload>(player);
        await CardPileCmd.Add(front, PileType.Hand);
        int handBeforeCompile = pcs.Hand.Cards.Count - 1; // excluding front itself
        await PlayCardAsync(front);
        AutoTestAssert.Equal(8, player.Creature.Block, "Frontload gained 8 Block");
        var frontFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "3rd encoded card compiled Function");
        AutoTestAssert.True(frontFunc.Keywords.Contains(CardKeyword.Retain), "Frontload Compile granted Retain to Function");
        AutoTestAssert.Equal(handBeforeCompile + 2, pcs.Hand.Cards.Count, "CutThrough Compile drew 1 card + FunctionCard added to Hand");

        // 6. Overheat (18 dmg + RemoveNextErrorPower) cancels OilSpill's Compile Error (no Slimed inserted!),
        //    whereas a 2nd OilSpill WITHOUT RemoveNextErrorPower DOES insert a Slimed!
        await ClearHandAndRefillAsync();
        int hpBeforeOver = enemy.CurrentHp;
        var overheat = combatState.CreateCard<Overheat>(player);
        await CardPileCmd.Add(overheat, PileType.Hand);
        await PlayCardAsync(overheat, enemy);
        AutoTestAssert.Equal(hpBeforeOver - 18, enemy.CurrentHp, "Overheat dealt 18 damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<RemoveNextErrorPower>(), "Overheat applied RemoveNextErrorPower(1)");

        int slimedBefore = pcs.DrawPile.Cards.Count(c => c is Slimed);
        var oil1 = (OilSpill)combatState.CreateCard<OilSpill>(player);
        await CardPileCmd.Add(oil1, PileType.Hand);
        await PlayCardAsync(oil1, enemy);
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<PoisonPower>(), "OilSpill #1 applied 4 Poison");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<RemoveNextErrorPower>(), "RemoveNextErrorPower consumed on encoding OilSpill #1");
        AutoTestAssert.True(!oil1.CompileEnabled, "RemoveNextErrorPower disabled OilSpill #1 Compile Error");

        await AutomatonMechanics.CompileFunctionAsync(ctx, player);
        AutoTestAssert.Equal(slimedBefore, pcs.DrawPile.Cards.Count(c => c is Slimed), "Canceled OilSpill #1 did NOT insert Slimed on Compile");

        // Now encode OilSpill #2 with NO RemoveNextErrorPower -> DOES insert Slimed on Compile!
        var oil2 = (OilSpill)combatState.CreateCard<OilSpill>(player);
        await CardPileCmd.Add(oil2, PileType.Hand);
        await PlayCardAsync(oil2, enemy);
        await AutomatonMechanics.CompileFunctionAsync(ctx, player);
        AutoTestAssert.Equal(slimedBefore + 1, pcs.DrawPile.Cards.Count(c => c is Slimed), "Uncanceled OilSpill #2 inserted Slimed on Compile");

        // 7. Deprecate (Weak), Invalidate (Vulnerable), DelayedGuard (+2 Dexterity -> BlockNextTurn(9)), DelayedSlice (9 dmg + DrawNextTurn), WhirlingStrike (6 AoE)!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<WeakPower>(enemy);
        await PowerCmd.Remove<VulnerablePower>(enemy);
        await PowerCmd.Apply<DexterityPower>(ctx, player.Creature, 2m, player.Creature, null);
        var dep = combatState.CreateCard<Deprecate>(player);
        var inv = combatState.CreateCard<Invalidate>(player);
        var dGuard = combatState.CreateCard<DelayedGuard>(player);
        await CardPileCmd.Add(dep, PileType.Hand);
        await CardPileCmd.Add(inv, PileType.Hand);
        await CardPileCmd.Add(dGuard, PileType.Hand);
        await PlayCardAsync(dep, enemy);
        await PlayCardAsync(inv, enemy);
        await PlayCardAsync(dGuard);
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "Deprecate applied 1 Weak");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Invalidate applied 1 Vulnerable");
        AutoTestAssert.Equal(9, player.Creature.GetPowerAmount<BlockNextTurnPower>(), "DelayedGuard scaled with +2 Dexterity (7 -> 9 BlockNextTurnPower)");
        await PowerCmd.Remove<DexterityPower>(player.Creature);
        await PowerCmd.Remove<VulnerablePower>(enemy);

        int hpBeforeSlice = enemy.CurrentHp;
        int drawNextBefore = player.Creature.GetPowerAmount<DrawCardsNextTurnPower>();
        var dSlice = combatState.CreateCard<DelayedSlice>(player);
        await CardPileCmd.Add(dSlice, PileType.Hand);
        await PlayCardAsync(dSlice, enemy);
        AutoTestAssert.Equal(hpBeforeSlice - 9, enemy.CurrentHp, "DelayedSlice dealt 9 damage");
        AutoTestAssert.Equal(drawNextBefore + 1, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "DelayedSlice applied DrawCardsNextTurnPower(1)");

        int hpBeforeWhirl = enemy.CurrentHp;
        var whirl = combatState.CreateCard<WhirlingStrike>(player);
        await CardPileCmd.Add(whirl, PileType.Hand);
        await PlayCardAsync(whirl);
        AutoTestAssert.Equal(hpBeforeWhirl - 6, enemy.CurrentHp, "WhirlingStrike dealt 6 AoE damage");
        ClearSequence();

        // 8. BronzeShield (Sticky Shield: Retain, 11 Block + Insert Slimed), BronzeTurbo (+2 Energy + Void in Discard),
        //    BronzeWildStrike (12 dmg + Insert Wound), WildBeam (Cleanse: 10 dmg + Exhaust random Status from Draw Pile)!
        await ClearHandAndRefillAsync();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int slimedBeforeShield = pcs.DrawPile.Cards.Count(c => c is Slimed);
        var bShield = combatState.CreateCard<BronzeShield>(player);
        AutoTestAssert.True(bShield.Keywords.Contains(CardKeyword.Retain), "BronzeShield has Retain");
        await CardPileCmd.Add(bShield, PileType.Hand);
        await PlayCardAsync(bShield);
        AutoTestAssert.Equal(11, player.Creature.Block, "BronzeShield gained 11 Block");
        AutoTestAssert.Equal(slimedBeforeShield + 1, pcs.DrawPile.Cards.Count(c => c is Slimed), "BronzeShield inserted Slimed");

        pcs.Energy = 3;
        int voidsBefore = pcs.DiscardPile.Cards.Count(c => c is MegaCrit.Sts2.Core.Models.Cards.Void);
        var bTurbo = combatState.CreateCard<BronzeTurbo>(player);
        await CardPileCmd.Add(bTurbo, PileType.Hand);
        await PlayCardAsync(bTurbo);
        AutoTestAssert.Equal(5, pcs.Energy, "BronzeTurbo gained +2 Energy");
        AutoTestAssert.Equal(voidsBefore + 1, pcs.DiscardPile.Cards.Count(c => c is MegaCrit.Sts2.Core.Models.Cards.Void), "BronzeTurbo added Void to Discard");

        int woundsBeforeWs = pcs.DrawPile.Cards.Count(c => c is Wound);
        int hpBeforeWs = enemy.CurrentHp;
        var bWs = combatState.CreateCard<BronzeWildStrike>(player);
        await CardPileCmd.Add(bWs, PileType.Hand);
        await PlayCardAsync(bWs, enemy);
        AutoTestAssert.Equal(hpBeforeWs - 12, enemy.CurrentHp, "BronzeWildStrike dealt 12 damage");
        AutoTestAssert.Equal(woundsBeforeWs + 1, pcs.DrawPile.Cards.Count(c => c is Wound), "BronzeWildStrike inserted Wound into Draw Pile");

        int drawStatusesBeforeCleanse = pcs.DrawPile.Cards.Count(c => c.Type == CardType.Status);
        int exhaustStatusesBeforeCleanse = pcs.ExhaustPile.Cards.Count(c => c.Type == CardType.Status);
        int hpBeforeCleanse = enemy.CurrentHp;
        var cleanse = combatState.CreateCard<WildBeam>(player);
        await CardPileCmd.Add(cleanse, PileType.Hand);
        await PlayCardAsync(cleanse, enemy);
        AutoTestAssert.Equal(hpBeforeCleanse - 10, enemy.CurrentHp, "WildBeam (Cleanse) dealt 10 damage");
        AutoTestAssert.Equal(drawStatusesBeforeCleanse - 1, pcs.DrawPile.Cards.Count(c => c.Type == CardType.Status), "WildBeam removed 1 Status from Draw Pile");
        AutoTestAssert.Equal(exhaustStatusesBeforeCleanse + 1, pcs.ExhaustPile.Cards.Count(c => c.Type == CardType.Status), "WildBeam Exhausted 1 Status from Draw Pile");
    }

    internal static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTO_UNC1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1500);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        void ClearSequence()
        {
            foreach (var orb in AutomatonMechanics.GetEncodedOrbs(player))
                AutomatonMechanics.RemoveEncodedOrb(player, orb);
        }

        // 1. Allocate: gains 1 Energy per Status in Draw Pile
        await ClearHandAndRefillAsync();
        await AutomatonMechanics.InsertCardIntoDrawPileAsync<Dazed>(player, 2);
        await AutomatonMechanics.InsertCardIntoDrawPileAsync<Wound>(player, 1);
        int statusCount = pcs.DrawPile.Cards.Count(c => c.Type == CardType.Status);
        AutoTestAssert.True(statusCount >= 3, "At least 3 Statuses in Draw Pile");
        pcs.Energy = 2;
        var alloc = combatState.CreateCard<Allocate>(player);
        await CardPileCmd.Add(alloc, PileType.Hand);
        await PlayCardAsync(alloc);
        AutoTestAssert.Equal(2 - 1 + statusCount, pcs.Energy, "Allocate gained 1 Energy per Status in Draw Pile");

        // 2. Backtrace (Innate, Compile Error Exhaust) + DigitalCarnage (Compile Error Ethereal) + AutomatonBronzeArmor (Artifact, Compile Error enemy Block)
        //    Also track an existing ForceShield in Draw Pile (cost 4 -> 3 after 1st compile, 3 -> 2 after 2nd compile)!
        await ClearHandAndRefillAsync();
        ClearSequence();
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        var existingFs = combatState.CreateCard<ForceShield>(player);
        await CardPileCmd.AddGeneratedCardToCombat(existingFs, PileType.Draw, player);
        AutoTestAssert.Equal(4, existingFs.EnergyCost.GetWithModifiers(CostModifiers.All), "Existing ForceShield starts at cost 4");

        var bt = combatState.CreateCard<Backtrace>(player);
        var dc = combatState.CreateCard<DigitalCarnage>(player);
        var ba = combatState.CreateCard<AutomatonBronzeArmor>(player);
        AutoTestAssert.True(bt.Keywords.Contains(CardKeyword.Innate), "Backtrace is Innate");
        await CardPileCmd.Add(bt, PileType.Hand);
        await CardPileCmd.Add(dc, PileType.Hand);
        await CardPileCmd.Add(ba, PileType.Hand);
        await PlayCardAsync(bt, enemy);
        await PlayCardAsync(dc, enemy);
        await PlayCardAsync(ba);

        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ArtifactPower>(), "BronzeArmor granted 1 Artifact");
        AutoTestAssert.Equal(12, enemy.Block, "BronzeArmor Compile Error gave enemy 12 Block");
        var errFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "Compiled Function from 3 Error cards");
        AutoTestAssert.True(errFunc.Keywords.Contains(CardKeyword.Exhaust), "Backtrace Compile Error added Exhaust to Function");
        AutoTestAssert.True(errFunc.Keywords.Contains(CardKeyword.Ethereal), "DigitalCarnage Compile Error added Ethereal to Function");
        AutoTestAssert.Equal(3, existingFs.EnergyCost.GetWithModifiers(CostModifiers.All), "Existing ForceShield discounted 4 -> 3 after 1st compile");

        // 3. CleanCode (RemoveNextErrorPower(3)): cancels Backtrace + DigitalCarnage + AutomatonBronzeArmor Compile Errors!
        await ClearHandAndRefillAsync();
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);
        var cleanCode = combatState.CreateCard<CleanCode>(player);
        await CardPileCmd.Add(cleanCode, PileType.Hand);
        await PlayCardAsync(cleanCode);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<RemoveNextErrorPower>(), "CleanCode applied RemoveNextErrorPower(3)");

        var bt2 = combatState.CreateCard<Backtrace>(player);
        var dc2 = combatState.CreateCard<DigitalCarnage>(player);
        var ba2 = combatState.CreateCard<AutomatonBronzeArmor>(player);
        await CardPileCmd.Add(bt2, PileType.Hand);
        await CardPileCmd.Add(dc2, PileType.Hand);
        await CardPileCmd.Add(ba2, PileType.Hand);
        await PlayCardAsync(bt2, enemy);
        await PlayCardAsync(dc2, enemy);
        await PlayCardAsync(ba2);
        var cleanFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "CleanCode compiled Function");
        AutoTestAssert.True(!cleanFunc.Keywords.Contains(CardKeyword.Exhaust), "CleanCode canceled Backtrace Exhaust Error");
        AutoTestAssert.True(!cleanFunc.Keywords.Contains(CardKeyword.Ethereal), "CleanCode canceled DigitalCarnage Ethereal Error");
        AutoTestAssert.Equal(0, enemy.Block, "CleanCode canceled BronzeArmor enemy Block Error");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<RemoveNextErrorPower>(), "All 3 RemoveNextErrorPower stacks consumed");
        AutoTestAssert.Equal(2, existingFs.EnergyCost.GetWithModifiers(CostModifiers.All), "Existing ForceShield discounted 3 -> 2 after 2nd compile");

        // Mid-combat generated ForceShield at compiled == 2 enters at cost 2!
        var midCombatFs = combatState.CreateCard<ForceShield>(player);
        await CardPileCmd.AddGeneratedCardToCombat(midCombatFs, PileType.Hand, player);
        AutoTestAssert.Equal(2, midCombatFs.EnergyCost.GetWithModifiers(CostModifiers.All), "Mid-combat ForceShield entered at cost 2 (4 - 2)");

        // 4. Constructor (Slot 0: 5->10 Block & compiles solo as 'Greatness()'!) + CloningBeam isolation (Slot 1 stays 5 Block!)
        await ClearHandAndRefillAsync();
        await CardPileCmd.Add(midCombatFs, PileType.Hand);
        var soloCtor = (Constructor)combatState.CreateCard<Constructor>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, soloCtor);
        var greatnessFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled solo Slot-0 Constructor");
        AutoTestAssert.Equal("Greatness()", greatnessFunc.Title, "Solo Slot-0 Constructor compiled as 'Greatness()'");
        AutoTestAssert.Equal(1, midCombatFs.EnergyCost.GetWithModifiers(CostModifiers.All), "Mid-combat ForceShield dropped 2 -> 1 after 3rd compile");
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await PlayCardAsync(midCombatFs);
        AutoTestAssert.Equal(12, player.Creature.Block, "Playing ForceShield granted 12 Block");

        var cloningBeam = combatState.CreateCard<CloningBeam>(player);
        await CardPileCmd.Add(cloningBeam, PileType.Hand);
        await PlayCardAsync(cloningBeam, enemy);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<CloningPower>(), "CloningBeam applied CloningPower(1)");

        var ctor = combatState.CreateCard<Constructor>(player);
        await CardPileCmd.Add(ctor, PileType.Hand);
        await PlayCardAsync(ctor);
        var ctorOrbs = AutomatonMechanics.GetEncodedOrbs(player);
        AutoTestAssert.Equal(2, ctorOrbs.Count, "CloningPower encoded 2 copies of Constructor (slots 0 & 1)");
        AutoTestAssert.Equal(10, ctorOrbs[0].EncodedCard!.DynamicVars.Block.IntValue, "Slot 0 Constructor gained +5 Block (5 -> 10)");
        AutoTestAssert.Equal(5, ctorOrbs[1].EncodedCard!.DynamicVars.Block.IntValue, "Slot 1 cloned Constructor did NOT inherit Slot 0's +5 Block (stayed 5!)");
        ClearSequence();

        // Test CloningPower on Slot 2 (3rd slot!) via ForLoop+: compiles 1st Function BEFORE encoding extra copies into new sequence!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);
        var boost = (Boost)combatState.CreateCard<Boost>(player);
        var fortify = (Fortify)combatState.CreateCard<Fortify>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, boost);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, fortify);

        pcs.Energy = 1;
        var forLoopPlus = combatState.CreateCard<ForLoop>(player);
        CardCmd.Upgrade(forLoopPlus); // X+1 = 1+1 = 2 extra copies!
        await CardPileCmd.Add(forLoopPlus, PileType.Hand);
        await PlayCardAsync(forLoopPlus);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<CloningPower>(), "ForLoop+ at 1 Energy applied CloningPower(2)");

        pcs.Energy = 5;
        var frag = combatState.CreateCard<FormatEncoded>(player);
        await CardPileCmd.Add(frag, PileType.Hand);
        await PlayCardAsync(frag, enemy);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Boost Compile granted +2 Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "Fortify Compile granted +2 Dexterity");
        AutoTestAssert.Equal(2, AutomatonMechanics.GetEncodedOrbs(player).Count, "CloningPower(2) on Slot 2 compiled Function first, then encoded 2 fresh FormatEncoded copies!");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<FunctionCard>().Any(), "FunctionCard added to Hand");
        ClearSequence();
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);

        // 5. ClassDefault (DefaultPower): re-encodes copy of Slot 0 card after compiling a Function!
        await ClearHandAndRefillAsync();
        var clsDef = combatState.CreateCard<ClassDefault>(player);
        await CardPileCmd.Add(clsDef, PileType.Hand);
        await PlayCardAsync(clsDef);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DefaultPower>(), "ClassDefault applied DefaultPower(2)");
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<FormatEncoded>(player));
        await AutomatonMechanics.CompileFunctionAsync(ctx, player);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DefaultPower>(), "DefaultPower decremented 2 -> 1");
        AutoTestAssert.Equal(1, AutomatonMechanics.GetEncodedOrbs(player).Count, "DefaultPower re-encoded a copy of Slot 0 card");
        AutoTestAssert.True(AutomatonMechanics.GetEncodedCards(player)[0] is FormatEncoded, "Re-encoded Slot 0 card is FormatEncoded");
        await PowerCmd.Remove<DefaultPower>(player.Creature);
        ClearSequence();

        // 6. ForceShield (discounts by FunctionsCompiledThisCombat for BOTH existing & newly created copies!) + FollowUp (plays twice after FunctionCard!)
        await ClearHandAndRefillAsync();
        int compiledNow = AutomatonMechanics.GetFunctionsCompiledThisCombat(player);
        AutoTestAssert.True(compiledNow >= 3, $"At least 3 Functions compiled ({compiledNow})");
        var fs = combatState.CreateCard<ForceShield>(player);
        await CardPileCmd.AddGeneratedCardToCombat(fs, PileType.Hand, player);
        AutoTestAssert.Equal(Math.Max(0, 4 - compiledNow), fs.EnergyCost.GetWithModifiers(CostModifiers.All), "ForceShield discounted immediately on entering combat");

        // Play FollowUp when last card was NOT a Function -> plays ONCE (4 Block, 7 dmg)!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var filler = combatState.CreateCard<BronzeStrike>(player);
        await CardPileCmd.Add(filler, PileType.Hand);
        await PlayCardAsync(filler, enemy);
        int hpBeforeFu1 = enemy.CurrentHp;
        var fu1 = combatState.CreateCard<FollowUp>(player);
        await CardPileCmd.Add(fu1, PileType.Hand);
        await PlayCardAsync(fu1, enemy);
        AutoTestAssert.Equal(hpBeforeFu1 - 7, enemy.CurrentHp, "FollowUp without prior Function played once (7 dmg)");
        AutoTestAssert.Equal(4, player.Creature.Block, "FollowUp without prior Function gained 4 Block");

        // Now play a FunctionCard, then FollowUp -> plays TWICE (8 Block, 14 dmg)!
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Safeguard>(player));
        var seqFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled Function for FollowUp");
        await PlayCardAsync(seqFunc);
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int hpBeforeFu2 = enemy.CurrentHp;
        var fu2 = combatState.CreateCard<FollowUp>(player);
        await CardPileCmd.Add(fu2, PileType.Hand);
        await PlayCardAsync(fu2, enemy);
        AutoTestAssert.Equal(hpBeforeFu2 - 14, enemy.CurrentHp, "FollowUp after FunctionCard played TWICE (14 dmg)");
        AutoTestAssert.Equal(8, player.Creature.Block, "FollowUp after FunctionCard gained 8 Block");

        // 7. Flail (7x2 AoE + 1 Artifact), BronzeOrb (Innate, 8 dmg + Encodes random Encodable from Draw Pile), GetLatest (0-cost random Encodable in Hand)
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<ArtifactPower>(player.Creature);
        int hpBeforeFlail = enemy.CurrentHp;
        var flail = combatState.CreateCard<Flail>(player);
        await CardPileCmd.Add(flail, PileType.Hand);
        await PlayCardAsync(flail);
        AutoTestAssert.Equal(hpBeforeFlail - 14, enemy.CurrentHp, "Flail dealt 7 x 2 = 14 AoE damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ArtifactPower>(), "Flail granted 1 Artifact");

        ClearSequence();
        var drawEnc = combatState.CreateCard<MinorBeam>(player);
        await CardPileCmd.Add(drawEnc, PileType.Draw, CardPilePosition.Top);
        int hpBeforeOrb = enemy.CurrentHp;
        var bronzeOrb = combatState.CreateCard<AutomatonBronzeOrb>(player);
        AutoTestAssert.True(bronzeOrb.Keywords.Contains(CardKeyword.Innate), "BronzeOrb is Innate");
        await CardPileCmd.Add(bronzeOrb, PileType.Hand);
        await PlayCardAsync(bronzeOrb, enemy);
        AutoTestAssert.Equal(hpBeforeOrb - 8, enemy.CurrentHp, "BronzeOrb dealt 8 damage");
        AutoTestAssert.Equal(1, AutomatonMechanics.GetEncodedOrbs(player).Count, "BronzeOrb encoded an Encodable card from Draw Pile");
        ClearSequence();

        await ClearHandAndRefillAsync();
        var getLatest = combatState.CreateCard<GetLatest>(player);
        await CardPileCmd.Add(getLatest, PileType.Hand);
        await PlayCardAsync(getLatest);
        var latestCard = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeCard>().FirstOrDefault(), "GetLatest added a BronzeCard to Hand");
        AutoTestAssert.True(latestCard.Encodes, "GetLatest generated an Encodable card");
        if (!latestCard.EnergyCost.CostsX)
            AutoTestAssert.Equal(0, latestCard.EnergyCost.GetWithModifiers(CostModifiers.All), "GetLatest card costs 0 until played");

        // 8. InfiniteBeams + InfiniteBeams+ across real turn end -> Turn 2 Hand receives 1 MinorBeam AND 1 MinorBeam+!
        await ClearHandAndRefillAsync();
        var ib = combatState.CreateCard<InfiniteBeams>(player);
        var ibPlus = combatState.CreateCard<InfiniteBeams>(player);
        CardCmd.Upgrade(ibPlus);
        await CardPileCmd.Add(ib, PileType.Hand);
        await CardPileCmd.Add(ibPlus, PileType.Hand);
        await PlayCardAsync(ib);
        await PlayCardAsync(ibPlus);

        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is MinorBeam { IsUpgraded: false }), "InfiniteBeamsPower added unupgraded MinorBeam on Turn 2");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is MinorBeam { IsUpgraded: true }), "InfiniteBeamsPlusPower added upgraded MinorBeam+ on Turn 2");

        // Verify InfiniteBeamsPower overflows cleanly to Discard when Hand is already full (10 cards)!
        while (pcs.Hand.Cards.Count < CardPile.MaxCardsInHand)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Hand);
        int discardBeamsBefore = pcs.DiscardPile.Cards.Count(c => c is MinorBeam);
        var ibPower = AutoTestAssert.NotNull(player.Creature.GetPower<InfiniteBeamsPower>(), "InfiniteBeamsPower active");
        await ibPower.AfterPlayerTurnStart(ctx, player);
        AutoTestAssert.Equal(CardPile.MaxCardsInHand, pcs.Hand.Cards.Count, "InfiniteBeamsPower did not exceed MaxCardsInHand");
        AutoTestAssert.Equal(discardBeamsBefore + 1, pcs.DiscardPile.Cards.Count(c => c is MinorBeam), "InfiniteBeamsPower overflowed MinorBeam to Discard when Hand full");
    }

    internal static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTO_UNC2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1500);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        void ClearSequence()
        {
            foreach (var orb in AutomatonMechanics.GetEncodedOrbs(player))
                AutomatonMechanics.RemoveEncodedOrb(player, orb);
        }

        // 1. Perfection() Easter Egg: Constructor (Slot 0: 5->10 Block) + Separator (Slot 1: 6->12 dmg) + Terminator (Slot 2: PlayTwice!)
        await ClearHandAndRefillAsync();
        ClearSequence();
        var perfCtor = combatState.CreateCard<Constructor>(player);
        var perfSep = combatState.CreateCard<Downfall.Automaton.Cards.Separator>(player);
        var perfTerm = combatState.CreateCard<Terminator>(player);
        await CardPileCmd.Add(perfCtor, PileType.Hand);
        await CardPileCmd.Add(perfSep, PileType.Hand);
        await CardPileCmd.Add(perfTerm, PileType.Hand);
        await PlayCardAsync(perfCtor);
        await PlayCardAsync(perfSep, enemy);
        await PlayCardAsync(perfTerm);

        var perfFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "Perfection() FunctionCard created");
        AutoTestAssert.Equal("Perfection()", perfFunc.Title, "Constructor + Separator + Terminator compiled as 'Perfection()'");
        AutoTestAssert.True(perfFunc.PlayTwice, "Terminator in Slot 2 set PlayTwice on FunctionCard");

        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int hpBeforePerf = enemy.CurrentHp;
        await PlayCardAsync(perfFunc, enemy);
        AutoTestAssert.Equal(20, player.Creature.Block, "Perfection() played twice: 2 x 10 = 20 Block from Slot-0 Constructor");
        AutoTestAssert.Equal(hpBeforePerf - 24, enemy.CurrentHp, "Perfection() played twice: 2 x 12 = 24 damage from Slot-1 Separator");

        // Verify Terminator in Slot 0 does NOT grant PlayTwice!
        await ClearHandAndRefillAsync();
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Terminator>(player));
        var nonLastTermFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled Slot-0 Terminator");
        AutoTestAssert.True(!nonLastTermFunc.PlayTwice, "Terminator in Slot 0 did NOT set PlayTwice");

        // 2. InfiniteLoop (6 dmg; Compile adds copy to Hand with +2 dmg = 8) + Iterate (2x3=6 dmg) + Shell+ (Blockchain+: Blur on play & Compile)!
        await ClearHandAndRefillAsync();
        int hpBeforeLoop = enemy.CurrentHp;
        var loop = combatState.CreateCard<InfiniteLoop>(player);
        var iterate = combatState.CreateCard<Iterate>(player);
        var shellPlus = combatState.CreateCard<Shell>(player);
        CardCmd.Upgrade(shellPlus);
        await CardPileCmd.Add(loop, PileType.Hand);
        await CardPileCmd.Add(iterate, PileType.Hand);
        await CardPileCmd.Add(shellPlus, PileType.Hand);
        await PlayCardAsync(loop, enemy);
        await PlayCardAsync(iterate, enemy);
        await PlayCardAsync(shellPlus);

        AutoTestAssert.Equal(hpBeforeLoop - 12, enemy.CurrentHp, "InfiniteLoop (6) + Iterate (2x3=6) dealt 12 total damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<BlurPower>(), "Shell+ granted 1 Blur on play + 1 Blur on Compile = 2 Blur");
        var scaledLoop = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<InfiniteLoop>().FirstOrDefault(), "InfiniteLoop Compile added copy to Hand");
        AutoTestAssert.Equal(8, scaledLoop.DynamicVars.Damage.IntValue, "InfiniteLoop Compile copy increased damage 6 -> 8");

        // 3. Optimize (OptimizePower(3)): upgrades upgradable encoded cards & skips already-upgraded cards!
        await ClearHandAndRefillAsync();
        var opt = combatState.CreateCard<Optimize>(player);
        await CardPileCmd.Add(opt, PileType.Hand);
        await PlayCardAsync(opt);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<OptimizePower>(), "Optimize applied OptimizePower(3)");

        var alreadyUpg = (BronzeCard)combatState.CreateCard<Safeguard>(player);
        CardCmd.Upgrade(alreadyUpg);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, alreadyUpg);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<OptimizePower>(), "OptimizePower did not consume stack on already-upgraded card");

        var rawNull = (NullPointer)combatState.CreateCard<NullPointer>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, rawNull);
        AutoTestAssert.True(rawNull.IsUpgraded, "OptimizePower upgraded NullPointer on Encode");
        AutoTestAssert.Equal(15, rawNull.DynamicVars.Damage.IntValue, "Upgraded NullPointer has 15 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<OptimizePower>(), "OptimizePower decremented 3 -> 2");

        // 4. Overload (Copy Paste): plays copies of encoded cards WITHOUT re-encoding them!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int hpBeforeOverload = enemy.CurrentHp;
        int orbCountBeforeOverload = AutomatonMechanics.GetEncodedOrbs(player).Count;
        var overload = combatState.CreateCard<Overload>(player);
        await CardPileCmd.Add(overload, PileType.Hand);
        await PlayCardAsync(overload);
        // Encoded cards: Safeguard+ (6 Block) + NullPointer+ (15 Block, 15 dmg) = 21 Block, 15 dmg!
        AutoTestAssert.Equal(21, player.Creature.Block, "Overload played copies of Safeguard+ (6) & NullPointer+ (15) = 21 Block");
        AutoTestAssert.Equal(hpBeforeOverload - 15, enemy.CurrentHp, "Overload played copy of NullPointer+ for 15 damage");
        AutoTestAssert.Equal(orbCountBeforeOverload, AutomatonMechanics.GetEncodedOrbs(player).Count, "Overload did NOT re-encode played copies");

        // Compile NullPointer -> FunctionCard is Unplayable!
        var nullFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled NullPointer Function");
        AutoTestAssert.True(nullFunc.Keywords.Contains(CardKeyword.Unplayable), "NullPointer Compile Error made Function Unplayable");
        AutoTestAssert.True(!nullFunc.CanPlay(), "Unplayable NullPointer Function cannot be played");
        await PowerCmd.Remove<OptimizePower>(player.Creature);

        // 5. Philosophize (+1 Str; Compile Error: enemy +2 Str) + Repair (4 Block; Compile: Heal 7 HP; non-generatable)!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<StrengthPower>(enemy);
        await CreatureCmd.Damage(ctx, player.Creature, 20m, ValueProp.Unblockable | ValueProp.Unpowered, enemy);
        int hpBeforeRepair = player.Creature.CurrentHp;

        var phil = combatState.CreateCard<Philosophize>(player);
        var repair = combatState.CreateCard<Repair>(player);
        AutoTestAssert.True(!repair.CanBeGeneratedInCombat, "Repair is excluded from combat random generation (Healing)");
        await CardPileCmd.Add(phil, PileType.Hand);
        await CardPileCmd.Add(repair, PileType.Hand);
        await PlayCardAsync(phil);
        await PlayCardAsync(repair);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Philosophize granted +1 Strength");
        await AutomatonMechanics.CompileFunctionAsync(ctx, player);
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<StrengthPower>(), "Philosophize Compile Error gave enemy +2 Strength");
        AutoTestAssert.Equal(hpBeforeRepair + 7, player.Creature.CurrentHp, "Repair Compile healed 7 HP");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<StrengthPower>(enemy);

        // 6. RecursiveStrike+ (9x2 dmg + Encodes 2 copies of BronzeStrike+)
        await ClearHandAndRefillAsync();
        ClearSequence();
        int hpBeforeRec = enemy.CurrentHp;
        var recStrike = combatState.CreateCard<RecursiveStrike>(player);
        CardCmd.Upgrade(recStrike);
        await CardPileCmd.Add(recStrike, PileType.Hand);
        await PlayCardAsync(recStrike, enemy);
        AutoTestAssert.Equal(hpBeforeRec - 18, enemy.CurrentHp, "RecursiveStrike+ dealt 9 x 2 = 18 damage");
        var recEncoded = AutomatonMechanics.GetEncodedCards(player);
        AutoTestAssert.Equal(2, recEncoded.Count, "RecursiveStrike+ encoded 2 cards");
        AutoTestAssert.True(recEncoded.All(c => c is BronzeStrike { IsUpgraded: true }), "RecursiveStrike+ encoded 2 copies of BronzeStrike+");
        ClearSequence();

        // 7. ItsAFeature (FeaturePower: +3 Block on draw or exhaust Status) + Refactor (Scry 4, Exhaust discarded Status -> +4 Block + FeaturePower +3 Block!) + Repulsor (ExhaustStatusesPower)
        await ClearHandAndRefillAsync();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var feat = combatState.CreateCard<ItsAFeature>(player);
        await CardPileCmd.Add(feat, PileType.Hand);
        await PlayCardAsync(feat);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<FeaturePower>(), "ItsAFeature applied FeaturePower(3)");

        var topDazed = combatState.CreateCard<Dazed>(player);
        await CardPileCmd.Add(topDazed, PileType.Draw, CardPilePosition.Top);
        var refactor = combatState.CreateCard<Refactor>(player);
        await CardPileCmd.Add(refactor, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topDazed)))
        {
            await PlayCardAsync(refactor);
        }
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(topDazed), "Refactor Exhausted discarded Status from Scry");
        AutoTestAssert.Equal(7, player.Creature.Block, "Refactor (+4 Block) + FeaturePower on Status Exhaust (+3 Block) = 7 Block");

        var repulsor = combatState.CreateCard<Repulsor>(player);
        await CardPileCmd.Add(repulsor, PileType.Hand);
        await PlayCardAsync(repulsor);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<Downfall.Guardian.Powers.ExhaustStatusesPower>(), "Repulsor applied Guardian ExhaustStatusesPower(1)");

        // Exercise FeaturePower on Status draw + Repulsor exhausting first Status drawn & drawing replacement!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var repulsorReplacement = combatState.CreateCard<BronzeStrike>(player);
        var repulsorDazed = combatState.CreateCard<Dazed>(player);
        await CardPileCmd.Add(repulsorReplacement, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(repulsorDazed, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1, player);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(repulsorDazed), "Repulsor exhausted drawn Status card");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(repulsorReplacement), "Repulsor drew replacement card");
        AutoTestAssert.Equal(6, player.Creature.Block, "FeaturePower(3) triggered on Status draw (+3) AND on Repulsor exhaust (+3) = 6 Block");

        // 8. Undervolt (ALL enemies -2 Strength + 2 Burns in Hand) + BronzeReturn (Discard -> top of Draw + EnergyNextTurnPower) + MaxOutput (Draw 3 + MaxOutputPower) across real turn transition!
        await ClearHandAndRefillAsync();
        var undervolt = combatState.CreateCard<Undervolt>(player);
        await CardPileCmd.Add(undervolt, PileType.Hand);
        await PlayCardAsync(undervolt);
        AutoTestAssert.Equal(-2, enemy.GetPowerAmount<StrengthPower>(), "Undervolt reduced enemy Strength by 2");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is Burn), "Undervolt added 2 Burns to Hand");

        await ClearHandAndRefillAsync();
        var returnTarget = combatState.CreateCard<BronzeStrike>(player);
        await CardPileCmd.Add(returnTarget, PileType.Discard);
        var bronzeReturn = combatState.CreateCard<BronzeReturn>(player);
        CardCmd.Upgrade(bronzeReturn); // +2 Energy next turn
        await CardPileCmd.Add(bronzeReturn, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, returnTarget)))
        {
            await PlayCardAsync(bronzeReturn);
        }
        AutoTestAssert.True(ReferenceEquals(pcs.DrawPile.Cards.FirstOrDefault(), returnTarget), "BronzeReturn put chosen Discard card on top of Draw Pile");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "BronzeReturn+ applied EnergyNextTurnPower(2)");

        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeDefend>(player), PileType.Draw, CardPilePosition.Top);
        int handBeforeMax = pcs.Hand.Cards.Count;
        var maxOut = combatState.CreateCard<MaxOutput>(player);
        await CardPileCmd.Add(maxOut, PileType.Hand);
        await PlayCardAsync(maxOut);
        AutoTestAssert.Equal(handBeforeMax + 3, pcs.Hand.Cards.Count, "MaxOutput immediately drew 3 cards");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MaxOutputPower>(), "MaxOutput applied MaxOutputPower(1)");

        int dazedBeforeTurn = pcs.DrawPile.Cards.Count(c => c is Dazed) + pcs.DiscardPile.Cards.Count(c => c is Dazed);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(5, pcs.Energy, "BronzeReturn+ EnergyNextTurnPower(2) granted 3 + 2 = 5 Energy on next turn");
        AutoTestAssert.True(pcs.Hand.Cards.Count >= 6, $"MaxOutputPower drew an extra card on Turn 2 (Hand={pcs.Hand.Cards.Count})");
        int dazedAfterTurn = pcs.DrawPile.Cards.Count(c => c is Dazed) + pcs.DiscardPile.Cards.Count(c => c is Dazed) + pcs.Hand.Cards.Count(c => c is Dazed) + pcs.ExhaustPile.Cards.Count(c => c is Dazed);
        AutoTestAssert.True(dazedAfterTurn > dazedBeforeTurn, "MaxOutputPower inserted a Dazed on Turn 2");
    }

    public static async Task TestRareCardsInCombat()
    {
        var (player, runState, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTOMATON_9D");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1500m);

        async Task PlayCardAsync(CardModel card, Creature? target = null)
        {
            await CardCmd.AutoPlay(ctx, card, target, AutoPlayType.Default);
        }

        async Task ClearHandAndRefillAsync(int energy = 20)
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = energy;
        }

        void ClearSequence()
        {
            foreach (var orb in AutomatonMechanics.GetEncodedOrbs(player).ToList())
                AutomatonMechanics.RemoveEncodedOrb(player, orb);
        }

        // 1. Verify all 17 Rare cards are registered in AutomatonCardPool and all 4 Dev Tools tokens are in TokenCardPool!
        var poolCards = ModelDb.CardPool<AutomatonCardPool>().AllCards.ToList();
        var rareTypes = new[]
        {
            typeof(Assembly), typeof(Downfall.Automaton.Cards.Break), typeof(BurnOut), typeof(ChosenStrike), typeof(FindAndReplace),
            typeof(Format), typeof(FullRelease), typeof(HardenedForm), typeof(HyperBeam), typeof(Library),
            typeof(Mutator), typeof(ShipIt), typeof(SpaghettiCode), typeof(ThunderWave), typeof(TinkerersToolbox),
            typeof(Verify), typeof(Virus)
        };
        foreach (var t in rareTypes)
            AutoTestAssert.True(poolCards.Any(c => c.GetType() == t), $"AutomatonCardPool missing Rare card {t.Name}");

        var tokenCards = ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.TokenCardPool>().AllCards.ToList();
        foreach (var t in new[] { typeof(Batch), typeof(ByteShift), typeof(Debug), typeof(Decompile) })
            AutoTestAssert.True(tokenCards.Any(c => c.GetType() == t), $"TokenCardPool missing Dev Tools token {t.Name}");

        // 2. HardenedForm (Summon Orb: +3 Block & 3 dmg on compile AND on play Function) + ChosenStrike (Cultist Strike permanent [SavedProperty] damage scaling on Compile!)
        await ClearHandAndRefillAsync();
        ClearSequence();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);

        var hardenedForm = combatState.CreateCard<HardenedForm>(player);
        await CardPileCmd.Add(hardenedForm, PileType.Hand);
        await PlayCardAsync(hardenedForm);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<HardenedFormPower>(), "HardenedForm applied HardenedFormPower(3)");

        var deckChosen = runState.CreateCard<ChosenStrike>(player);
        await CardPileCmd.Add(deckChosen, PileType.Deck);
        var chosenStrike = (ChosenStrike)combatState.CloneCard(deckChosen);
        chosenStrike.DeckVersion = deckChosen;
        AutoTestAssert.Equal(6, chosenStrike.DynamicVars.Damage.IntValue, "ChosenStrike starts at 6 base damage");

        int hpBeforeChosen = enemy.CurrentHp;
        await CardPileCmd.Add(chosenStrike, PileType.Hand);
        await PlayCardAsync(chosenStrike, enemy);
        AutoTestAssert.Equal(hpBeforeChosen - 6, enemy.CurrentHp, "ChosenStrike dealt 6 damage on play");

        // Compile via Batch (also tests Dev Tools Batch token + IsPlayable!)
        var batch = combatState.CreateCard<Batch>(player);
        AutoTestAssert.True(batch.CanPlay(), "Batch is playable when cards are Encoded");
        int hpBeforeCompile = enemy.CurrentHp;
        await CardPileCmd.Add(batch, PileType.Hand);
        await PlayCardAsync(batch);

        // ChosenStrike OnCompilePreEmbed bumped damage 6 -> 7 on both DeckVersion and compiled Function!
        AutoTestAssert.Equal(1, deckChosen.BonusDamage, "ChosenStrike permanently incremented DeckVersion BonusDamage to 1");
        AutoTestAssert.Equal(7, deckChosen.DynamicVars.Damage.IntValue, "ChosenStrike DeckVersion damage increased to 7");
        var chosenRoundTrip = (ChosenStrike)CardModel.FromSerializable(deckChosen.ToSerializable());
        AutoTestAssert.Equal(1, chosenRoundTrip.BonusDamage, "ChosenStrike [SavedProperty] BonusDamage round-trips");
        AutoTestAssert.Equal(7, chosenRoundTrip.DynamicVars.Damage.IntValue, "ChosenStrike round-tripped Damage is 7");

        // HardenedForm triggered on compile (+3 Block, 3 dmg)
        AutoTestAssert.Equal(3, player.Creature.Block, "HardenedFormPower triggered on Function compile (+3 Block)");
        AutoTestAssert.Equal(hpBeforeCompile - 3, enemy.CurrentHp, "HardenedFormPower dealt 3 damage on Function compile");

        var chosenFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "Batch compiled FunctionCard into Hand");
        int hpBeforeFuncPlay = enemy.CurrentHp;
        await PlayCardAsync(chosenFunc, enemy);
        // Function dealt 7 dmg (pre-embed permanent buff!) + HardenedForm triggered on Function play (+3 dmg, +3 Block)!
        AutoTestAssert.Equal(hpBeforeFuncPlay - 10, enemy.CurrentHp, "Compiled ChosenStrike Function dealt 7 + HardenedForm 3 = 10 damage");
        AutoTestAssert.Equal(6, player.Creature.Block, "HardenedFormPower triggered on Function play (+3 more Block = 6)");
        AutoTestAssert.True(!combatState.CreateCard<Batch>(player).CanPlay(), "Batch is unplayable when sequence is empty");
        await PowerCmd.Remove<HardenedFormPower>(player.Creature);

        // Multi-clone ChosenStrike compile sharing DeckVersion: both encoded copies buff each other before embedding!
        var csA = (ChosenStrike)combatState.CloneCard(deckChosen);
        var csB = (ChosenStrike)combatState.CloneCard(deckChosen);
        csA.DeckVersion = deckChosen;
        csB.DeckVersion = deckChosen;
        await AutomatonMechanics.EncodeCardAsync(ctx, player, csA);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, csB);
        var multiCsFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled 2x ChosenStrike Function");
        AutoTestAssert.Equal(3, deckChosen.BonusDamage, "2x ChosenStrike compile incremented DeckVersion BonusDamage 1 -> 3");
        AutoTestAssert.Equal(9, multiCsFunc.EncodedCards[0].DynamicVars.Damage.IntValue, "First embedded ChosenStrike received both pre-embed buffs (9)");
        AutoTestAssert.Equal(9, multiCsFunc.EncodedCards[1].DynamicVars.Damage.IntValue, "Second embedded ChosenStrike received both pre-embed buffs (9)");
        var coldCsRoundTrip = (ChosenStrike)CardModel.FromSerializable(deckChosen.ToSerializable());
        AutoTestAssert.Equal(3, coldCsRoundTrip.BonusDamage, "Cold FromSerializable BonusDamage is 3");
        AutoTestAssert.Equal(9, coldCsRoundTrip.DynamicVars.Damage.IntValue, "Cold FromSerializable Damage is 6+3=9 (not double-counted)");

        // 3. Break (15 dmg + Compile Error 5 statuses) + Debug (strips Compile Error!) + ByteShift (returns all encoded with Retain) + Decompile (exhausts all encoded, +1 Energy & +1 draw per card)!
        await ClearHandAndRefillAsync();
        ClearSequence();
        var brk1 = combatState.CreateCard<Downfall.Automaton.Cards.Break>(player);
        await CardPileCmd.Add(brk1, PileType.Hand);
        await PlayCardAsync(brk1, enemy);
        var dbg = combatState.CreateCard<Debug>(player);
        await CardPileCmd.Add(dbg, PileType.Hand);
        await PlayCardAsync(dbg);
        await AutomatonMechanics.CompileFunctionAsync(ctx, player);
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count(c => c.Type == CardType.Status), "Debug suppressed Break's 5-Status Compile Error");

        // Now test Break WITHOUT Debug -> adds Dazed, Slimed, Wound, Burn, Void!
        await ClearHandAndRefillAsync();
        var brk2 = combatState.CreateCard<Downfall.Automaton.Cards.Break>(player);
        await CardPileCmd.Add(brk2, PileType.Hand);
        await PlayCardAsync(brk2, enemy);
        await AutomatonMechanics.CompileFunctionAsync(ctx, player);
        AutoTestAssert.Equal(5, pcs.Hand.Cards.Count(c => c.Type == CardType.Status), "Break Compile Error added all 5 Statuses to Hand");

        // ByteShift returns all Encoded cards to Hand with Retain
        await ClearHandAndRefillAsync();
        var bsCard1 = combatState.CreateCard<Safeguard>(player);
        var bsCard2 = combatState.CreateCard<Replicate>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, bsCard1);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, bsCard2);
        var byteShift = combatState.CreateCard<ByteShift>(player);
        await CardPileCmd.Add(byteShift, PileType.Hand);
        await PlayCardAsync(byteShift);
        AutoTestAssert.True(AutomatonMechanics.IsSequenceEmpty(player), "ByteShift emptied the sequence");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(bsCard1) && bsCard1.Keywords.Contains(CardKeyword.Retain), "ByteShift returned Safeguard with Retain");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(bsCard2) && bsCard2.Keywords.Contains(CardKeyword.Retain), "ByteShift returned Replicate with Retain");

        // Decompile exhausts all Encoded cards, +1 Energy & +1 draw each
        await ClearHandAndRefillAsync(energy: 5);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Draw, CardPilePosition.Top);
        var decCard1 = combatState.CreateCard<Safeguard>(player);
        var decCard2 = combatState.CreateCard<BronzeDefend>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, decCard1);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, decCard2);
        var decompile = combatState.CreateCard<Decompile>(player);
        await CardPileCmd.Add(decompile, PileType.Hand);
        await PlayCardAsync(decompile);
        AutoTestAssert.True(AutomatonMechanics.IsSequenceEmpty(player), "Decompile emptied the sequence");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(decCard1) && pcs.ExhaustPile.Cards.Contains(decCard2), "Decompile exhausted both Encoded cards");
        AutoTestAssert.Equal(7, pcs.Energy, "Decompile gained +2 Energy (5 -> 7)");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Decompile drew 2 cards");

        // 4. TinkerersToolbox (Dev Tools -> chooses 1 of 4 tokens) + Format (X-cost: Encode X+1 Fragments + gain 1 Energy) + SpaghettiCode (compiles a Function!)
        await ClearHandAndRefillAsync();
        var toolbox = combatState.CreateCard<TinkerersToolbox>(player);
        await CardPileCmd.Add(toolbox, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is Debug))
        {
            await PlayCardAsync(toolbox);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Debug), "TinkerersToolbox added chosen Dev Tools token (Debug) to Hand");

        await ClearHandAndRefillAsync(energy: 2);
        ClearSequence();
        var format = combatState.CreateCard<Format>(player);
        CardCmd.Upgrade(format); // X+1 = 2+1 = 3 Fragments -> compiles immediately! +1 Energy!
        await CardPileCmd.Add(format, PileType.Hand);
        AutoTestAssert.True(format.TryManualPlay(null), "Manually play Format+ with X=2 Energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Energy, "Format spent X=2 Energy and gained 1 Energy back");
        var fmtFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "Format+ with X=2 encoded 3 Fragments and compiled a Function");
        AutoTestAssert.Equal(3, fmtFunc.EncodedCards.Count, "Format+ Function contains 3 Fragments");
        AutoTestAssert.True(fmtFunc.EncodedCards.All(c => c is FormatEncoded), "All 3 encoded cards in Format+ Function are FormatEncoded");

        await ClearHandAndRefillAsync();
        ClearSequence();
        int compiledBeforeSpaghetti = AutomatonCombatState.Get(combatState).FunctionsCompiledThisCombat;
        var spaghetti = combatState.CreateCard<SpaghettiCode>(player);
        await CardPileCmd.Add(spaghetti, PileType.Hand);
        await PlayCardAsync(spaghetti);
        AutoTestAssert.Equal(compiledBeforeSpaghetti + 1, AutomatonCombatState.Get(combatState).FunctionsCompiledThisCombat, "SpaghettiCode compiled a Function");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<FunctionCard>().Any(), "SpaghettiCode added compiled Function to Hand");

        // 5. Assembly (Scry 5, encodes discarded Encodable cards in order, discards non-Encodable) + FindAndReplace (fetch from Draw + insert Dazed)
        await ClearHandAndRefillAsync();
        ClearSequence();
        var asmEnc = combatState.CreateCard<Safeguard>(player);
        var asmNon = combatState.CreateCard<BronzeStrike>(player);
        await CardPileCmd.Add(asmNon, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(asmEnc, PileType.Draw, CardPilePosition.Top);
        var assembly = combatState.CreateCard<Assembly>(player);
        await CardPileCmd.Add(assembly, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, asmEnc) || ReferenceEquals(c, asmNon)))
        {
            await PlayCardAsync(assembly);
        }
        AutoTestAssert.True(AutomatonMechanics.GetEncodedCards(player).Contains(asmEnc), "Assembly Encoded discarded Encodable card");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(asmNon), "Assembly discarded non-Encodable card");
        ClearSequence();

        await ClearHandAndRefillAsync();
        var farTarget = combatState.CreateCard<BronzeDefend>(player);
        await CardPileCmd.Add(farTarget, PileType.Draw, CardPilePosition.Top);
        int dazedInDrawBefore = pcs.DrawPile.Cards.Count(c => c is Dazed);
        var far = combatState.CreateCard<FindAndReplace>(player);
        await CardPileCmd.Add(far, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, farTarget)))
        {
            await PlayCardAsync(far);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(farTarget), "FindAndReplace fetched chosen card from Draw Pile to Hand");
        AutoTestAssert.Equal(dazedInDrawBefore + 1, pcs.DrawPile.Cards.Count(c => c is Dazed), "Unupgraded FindAndReplace inserted a Dazed into Draw Pile");

        // FindAndReplace from Discard Pile (unupgraded adds Dazed to Discard) & FindAndReplace+ (adds NO Dazed!)
        var farDiscardTarget = combatState.CreateCard<BronzeStrike>(player);
        await CardPileCmd.Add(farDiscardTarget, PileType.Discard);
        int dazedInDiscardBefore = pcs.DiscardPile.Cards.Count(c => c is Dazed);
        var far2 = combatState.CreateCard<FindAndReplace>(player);
        await CardPileCmd.Add(far2, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, farDiscardTarget)))
        {
            await PlayCardAsync(far2);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(farDiscardTarget), "FindAndReplace fetched chosen card from Discard Pile");
        AutoTestAssert.Equal(dazedInDiscardBefore + 1, pcs.DiscardPile.Cards.Count(c => c is Dazed), "Unupgraded FindAndReplace added a Dazed to Discard Pile");

        var farUpTarget = combatState.CreateCard<BronzeDefend>(player);
        await CardPileCmd.Add(farUpTarget, PileType.Discard);
        int dazedInDiscardBeforeUp = pcs.DiscardPile.Cards.Count(c => c is Dazed);
        var farUp = combatState.CreateCard<FindAndReplace>(player);
        CardCmd.Upgrade(farUp);
        await CardPileCmd.Add(farUp, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, farUpTarget)))
        {
            await PlayCardAsync(farUp);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(farUpTarget), "FindAndReplace+ fetched chosen card from Discard Pile");
        AutoTestAssert.Equal(dazedInDiscardBeforeUp, pcs.DiscardPile.Cards.Count(c => c is Dazed), "FindAndReplace+ added NO Dazed");

        // 6. HyperBeam (25 AoE + 5 Voids on TOP of Draw Pile) + ThunderWave (12 AoE + 1 Buffer) + BurnOut (exhaust all Statuses & 6 dmg each) + ShipIt (5 dmg + repeat per exhausted Status)
        await ClearHandAndRefillAsync();
        int hpBeforeHyper = enemy.CurrentHp;
        var hyperBeam = combatState.CreateCard<HyperBeam>(player);
        await CardPileCmd.Add(hyperBeam, PileType.Hand);
        await PlayCardAsync(hyperBeam);
        AutoTestAssert.Equal(hpBeforeHyper - 25, enemy.CurrentHp, "HyperBeam dealt 25 AoE damage");
        AutoTestAssert.Equal(5, pcs.DrawPile.Cards.Take(5).Count(c => c is MegaCrit.Sts2.Core.Models.Cards.Void), "HyperBeam placed 5 Voids on TOP of Draw Pile");

        int hpBeforeThunder = enemy.CurrentHp;
        var thunder = combatState.CreateCard<ThunderWave>(player);
        await CardPileCmd.Add(thunder, PileType.Hand);
        await PlayCardAsync(thunder);
        AutoTestAssert.Equal(hpBeforeThunder - 12, enemy.CurrentHp, "ThunderWave dealt 12 AoE damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<BufferPower>(), "ThunderWave granted 1 BufferPower");

        int totalStatusesBeforeBurnOut = pcs.DrawPile.Cards.Count(c => c.Type == CardType.Status)
            + pcs.DiscardPile.Cards.Count(c => c.Type == CardType.Status)
            + pcs.Hand.Cards.Count(c => c.Type == CardType.Status);
        AutoTestAssert.True(totalStatusesBeforeBurnOut >= 5, "At least 5 Statuses exist before BurnOut");
        int hpBeforeBurnOut = enemy.CurrentHp;
        var burnOut = combatState.CreateCard<BurnOut>(player);
        await CardPileCmd.Add(burnOut, PileType.Hand);
        await PlayCardAsync(burnOut);
        AutoTestAssert.Equal(hpBeforeBurnOut - (6 * totalStatusesBeforeBurnOut), enemy.CurrentHp, "BurnOut dealt 6 damage per Status across Draw/Discard/Hand");
        AutoTestAssert.Equal(0, pcs.DrawPile.Cards.Count(c => c.Type == CardType.Status) + pcs.DiscardPile.Cards.Count(c => c.Type == CardType.Status) + pcs.Hand.Cards.Count(c => c.Type == CardType.Status), "BurnOut exhausted ALL Statuses");

        int exhaustedStatuses = pcs.ExhaustPile.Cards.Count(c => c.Type == CardType.Status);
        AutoTestAssert.True(exhaustedStatuses >= 5, "Exhaust Pile holds exhausted Statuses for ShipIt");
        int hpBeforeShipIt = enemy.CurrentHp;
        var shipIt = combatState.CreateCard<ShipIt>(player);
        await CardPileCmd.Add(shipIt, PileType.Hand);
        await PlayCardAsync(shipIt, enemy);
        AutoTestAssert.Equal(hpBeforeShipIt - (5 * (1 + exhaustedStatuses)), enemy.CurrentHp, "ShipIt hit 1 + Exhausted Status count times");

        // 7. Mutator (+2 Strength, transforms Status in Hand into copy of Mutator) + Virus+ (6 dmg, discards hand, adds MinorBeam+ per card discarded)
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        var mutDazed = combatState.CreateCard<Dazed>(player);
        var mutator = combatState.CreateCard<Mutator>(player);
        CardCmd.Upgrade(mutator);
        await CardPileCmd.Add(mutDazed, PileType.Hand);
        await CardPileCmd.Add(mutator, PileType.Hand);
        await PlayCardAsync(mutator);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Mutator granted +2 Strength");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(mutDazed), "Mutator exhausted Status card in Hand");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Mutator { IsUpgraded: true }), "Mutator added an upgraded copy of itself to Hand");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        await ClearHandAndRefillAsync();
        await CardPileCmd.Add(combatState.CreateCard<BronzeDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<BronzeDefend>(player), PileType.Hand);
        int hpBeforeVirus = enemy.CurrentHp;
        var virus = combatState.CreateCard<Virus>(player);
        CardCmd.Upgrade(virus);
        await CardPileCmd.Add(virus, PileType.Hand);
        await PlayCardAsync(virus, enemy);
        AutoTestAssert.Equal(hpBeforeVirus - 6, enemy.CurrentHp, "Virus+ dealt 6 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is MinorBeam { IsUpgraded: true }), "Virus+ discarded 2 cards and added 2 MinorBeam+ to Hand");

        // 8. FullRelease (blocks 2nd FullRelease, compiles Power Function -> FullReleasePower fires at start of next turn) + Library (LibraryPower adds 0-cost Encodable) + Verify (VerifyPower Fine Tunes drawn Function)!
        await ClearHandAndRefillAsync();
        ClearSequence();
        var fr1 = combatState.CreateCard<FullRelease>(player);
        var fr2 = combatState.CreateCard<FullRelease>(player);
        var frGuard = combatState.CreateCard<Safeguard>(player); // 4 Block
        await CardPileCmd.Add(fr1, PileType.Hand);
        await CardPileCmd.Add(fr2, PileType.Hand);
        await CardPileCmd.Add(frGuard, PileType.Hand);
        await PlayCardAsync(fr1);
        AutoTestAssert.True(!fr2.CanPlay(), "Second FullRelease is unplayable while one is already Encoded");
        await PlayCardAsync(frGuard);

        var frFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled FullRelease Function");
        AutoTestAssert.Equal(CardType.Power, frFunc.Type, "FullRelease turned Function into a Power");
        AutoTestAssert.Equal(TargetType.Self, frFunc.TargetType, "FullRelease Function targets Self");
        AutoTestAssert.True(frFunc.Title.EndsWith("()"), $"FullRelease Function title valid: {frFunc.Title}");
        await PlayCardAsync(frFunc);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FullReleasePower>(), "Playing FullRelease Function applied FullReleasePower");

        // Verify canonical ModelDb.Power<FullReleasePower>() isolation AND stacking a 2nd FullRelease Function (+5 Block)!
        var freshCanonicalClone = (FullReleasePower)ModelDb.Power<FullReleasePower>().ToMutable();
        AutoTestAssert.Equal(
            "Trigger your compiled Function effects.",
            ((MegaCrit.Sts2.Core.Localization.DynamicVars.StringVar)freshCanonicalClone.DynamicVars["Effects"]).StringValue,
            "Canonical FullReleasePower singleton was NOT mutated by combat RegisterFunction");

        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<FullRelease>(player));
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<BronzeDefend>(player)); // 5 Block
        var frFunc2 = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled 2nd FullRelease Function");
        await PlayCardAsync(frFunc2);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<FullReleasePower>(), "Playing 2nd FullRelease Function stacked FullReleasePower to 2");

        var library = combatState.CreateCard<Library>(player);
        await CardPileCmd.Add(library, PileType.Hand);
        await PlayCardAsync(library);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<LibraryPower>(), "Library applied LibraryPower(1)");

        var verify = combatState.CreateCard<Verify>(player);
        AutoTestAssert.True(verify.Keywords.Contains(CardKeyword.Ethereal), "Unupgraded Verify is Ethereal");
        CardCmd.Upgrade(verify);
        AutoTestAssert.True(!verify.Keywords.Contains(CardKeyword.Ethereal), "Upgraded Verify removes Ethereal");
        await CardPileCmd.Add(verify, PileType.Hand);
        await PlayCardAsync(verify);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<VerifyPower>(), "Verify applied VerifyPower(1)");

        // Put a fresh Safeguard(4 Block) Function on top of Draw Pile to verify VerifyPower on real turn draw!
        var drawSub = combatState.CreateCard<Safeguard>(player);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, drawSub);
        var drawFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled Function for VerifyPower draw test");
        await CardPileCmd.Add(drawFunc, PileType.Draw, CardPilePosition.Top);
        AutoTestAssert.Equal(4, drawFunc.EncodedCards[0].DynamicVars.Block.IntValue, "Function starts at 4 Block before VerifyPower draw");

        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.True(player.Creature.Block >= 9, $"Stacked FullReleasePower granted 4 + 5 = 9 Block at start of Turn 2 (Block={player.Creature.Block})");
        AutoTestAssert.Equal(5, drawFunc.EncodedCards[0].DynamicVars.Block.IntValue, "VerifyPower Fine Tuned drawn Function from 4 -> 5 Block");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is BronzeCard { Encodes: true } && c.EnergyCost.GetWithModifiers(CostModifiers.All) == 0),
            "LibraryPower added a 0-cost Encodable card to Hand on Turn 2");
    }

    public static async Task TestRelicsAndPotionsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheAutomaton>("AUTOMATON_9E");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1500m);

        async Task PlayCardAsync(CardModel card, Creature? target = null)
        {
            await CardCmd.AutoPlay(ctx, card, target, AutoPlayType.Default);
        }

        async Task ClearHandAndRefillAsync(int energy = 20)
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = energy;
        }

        void ClearSequence()
        {
            foreach (var orb in AutomatonMechanics.GetEncodedOrbs(player).ToList())
                AutomatonMechanics.RemoveEncodedOrb(player, orb);
        }

        // 1. Verify RelicPool (10 total: 2 Starter + 8 Pool), PotionPool (4), and TokenCardPool (6 Good Statuses)!
        var relicPool = ModelDb.RelicPool<AutomatonRelicPool>().AllRelics.ToList();
        foreach (var t in new[]
        {
            typeof(BronzeCore), typeof(PlatinumCore), typeof(ProtectiveGoggles), typeof(SilverBullet),
            typeof(CableSpool), typeof(DecasWashers), typeof(Mallet), typeof(Timepiece),
            typeof(BronzeIdol), typeof(DonusWashers)
        })
        {
            AutoTestAssert.True(relicPool.Any(r => r.GetType() == t), $"AutomatonRelicPool missing {t.Name}");
        }
        AutoTestAssert.Equal(10, relicPool.Count, "AutomatonRelicPool has exact 10 relics (no non-upgrade Boss relics)");

        var potionPool = ModelDb.PotionPool<AutomatonPotionPool>().AllPotions.ToList();
        foreach (var t in new[]
        {
            typeof(Downfall.Automaton.Potions.FreeFunctionsPotion),
            typeof(Downfall.Automaton.Potions.BurnAndBuffPotion),
            typeof(Downfall.Automaton.Potions.CleanCodePotion),
            typeof(Downfall.Automaton.Potions.BuildAFunctionPotion)
        })
        {
            AutoTestAssert.True(potionPool.Any(p => p.GetType() == t), $"AutomatonPotionPool missing {t.Name}");
        }

        var tokenCards = ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.TokenCardPool>().AllCards.ToList();
        foreach (var t in new[]
        {
            typeof(BronzeDaze), typeof(BronzeGrievousWound), typeof(BronzeIgnite),
            typeof(BronzeIntoTheVoid), typeof(BronzeUsefulSlime), typeof(BronzeUnknownStatus)
        })
        {
            AutoTestAssert.True(tokenCards.Any(c => c.GetType() == t), $"TokenCardPool missing Good Status {t.Name}");
        }

        // 2. CableSpool (first Encodable card played encodes a copy!) + Timepiece (Functions gain Retain!)
        await ClearHandAndRefillAsync();
        ClearSequence();
        var cableSpool = await RelicCmd.Obtain<CableSpool>(player);
        var timepiece = await RelicCmd.Obtain<Timepiece>(player);
        AutoTestAssert.NotNull(cableSpool, "Obtained CableSpool");
        AutoTestAssert.NotNull(timepiece, "Obtained Timepiece");

        var firstEnc = combatState.CreateCard<Safeguard>(player);
        await CardPileCmd.Add(firstEnc, PileType.Hand);
        await PlayCardAsync(firstEnc);
        AutoTestAssert.Equal(2, AutomatonMechanics.GetEncodedOrbs(player).Count, "CableSpool encoded an extra copy of the first Encodable card played (2 total)");

        // Playing 2nd Encodable card compiles (3rd slot) and does NOT trigger CableSpool a second time!
        var secondEnc = combatState.CreateCard<Safeguard>(player);
        await CardPileCmd.Add(secondEnc, PileType.Hand);
        await PlayCardAsync(secondEnc);
        AutoTestAssert.Equal(0, AutomatonMechanics.GetEncodedOrbs(player).Count, "CableSpool did NOT trigger on 2nd Encodable card played");
        var retainedFunc = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<FunctionCard>().FirstOrDefault(), "Compiled Function from CableSpool + Safeguard");
        AutoTestAssert.True(retainedFunc.Keywords.Contains(CardKeyword.Retain), "Timepiece granted Retain to compiled Function");

        // 3. ProtectiveGoggles (+4 Block at end of turn ONLY when sequence is empty!) + SilverBullet + Mallet + DonusWashers + DecasWashers on combat turn start!
        await RelicCmd.Obtain<ProtectiveGoggles>(player);
        await RelicCmd.Obtain<SilverBullet>(player);
        await RelicCmd.Obtain<Mallet>(player);
        await RelicCmd.Obtain<DonusWashers>(player);
        var decas = await RelicCmd.Obtain<DecasWashers>(player);
        AutoTestAssert.NotNull(decas, "Obtained DecasWashers");

        // First test ProtectiveGoggles when sequence is NON-empty -> NO Block at turn end!
        await ClearHandAndRefillAsync();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Safeguard>(player));
        int roundBefore = combatState.RoundNumber;
        for (int i = 0; i < 10; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeStrike>(player), PileType.Draw, CardPilePosition.Top);

        // Next turn triggers SilverBullet, Mallet, DonusWashers, and DecasWashers (1st of 3 turns)!
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, roundBefore + 1);
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is WhirlingStrike && c.EnergyCost.GetWithModifiers(CostModifiers.All) == 0),
            "SilverBullet added 0-cost WhirlingStrike (Piercing Shot) to Hand");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is FineTuning { IsUpgraded: true }),
            "Mallet added 2 copies of FineTuning+ to Hand");
        int expectedDonusEnergy = pcs.Hand.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Void) ? 4 : 5;
        AutoTestAssert.Equal(expectedDonusEnergy, pcs.Energy, "DonusWashers granted +2 Energy (3 -> 5, or 4 if DecasWashers immediately drew the inserted Void)");
        AutoTestAssert.True(pcs.DrawPile.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Void)
            || pcs.Hand.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Void)
            || pcs.DiscardPile.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Void),
            "DonusWashers inserted a Void into Draw Pile");
        AutoTestAssert.Equal(2, decas.DisplayAmount, "DecasWashers drew an extra card and decremented remaining turns 3 -> 2");

        // Now test ProtectiveGoggles with EMPTY sequence -> grants 4 Block at end of turn (and verify via BeforeSideTurnEnd directly before enemy attack/block reset)!
        ClearSequence();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var goggles = player.Relics.OfType<ProtectiveGoggles>().First();
        await goggles.BeforeSideTurnEnd(ctx, CombatSide.Player, [player.Creature]);
        AutoTestAssert.Equal(4, player.Creature.Block, "ProtectiveGoggles granted +4 Block when ending turn with no Encoded cards");

        // With non-empty sequence, ProtectiveGoggles grants 0 additional Block
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Safeguard>(player));
        await goggles.BeforeSideTurnEnd(ctx, CombatSide.Player, [player.Creature]);
        AutoTestAssert.Equal(4, player.Creature.Block, "ProtectiveGoggles did NOT grant Block when cards are Encoded");
        ClearSequence();

        // 4. Potions: FreeFunctionsPotion (Machine Oil), CleanCodePotion (Alchyrical Elixir), BurnAndBuffPotion (Vexing Draught), BuildAFunctionPotion (Kio's Clever Concoction)!
        await ClearHandAndRefillAsync();
        var oilTarget = combatState.CreateCard<Safeguard>(player); // 4 Block
        await AutomatonMechanics.EncodeCardAsync(ctx, player, oilTarget);
        var oilProc = await PotionCmd.TryToProcure<Downfall.Automaton.Potions.FreeFunctionsPotion>(player);
        var oilPotion = AutoTestAssert.NotNull(oilProc.potion, "Procured FreeFunctionsPotion");
        await oilPotion.OnUseWrapper(ctx, null);
        AutoTestAssert.Equal(5, oilTarget.DynamicVars.Block.IntValue, "FreeFunctionsPotion (Machine Oil) Fine Tuned Encoded Safeguard 4 -> 5 Block");
        ClearSequence();

        // CleanCodePotion -> CleanCodePower(1) + FreeFunctionPower(1) -> strips Break Compile Error & makes Function cost 0!
        var ccProc = await PotionCmd.TryToProcure<Downfall.Automaton.Potions.CleanCodePotion>(player);
        var ccPotion = AutoTestAssert.NotNull(ccProc.potion, "Procured CleanCodePotion");
        await ccPotion.OnUseWrapper(ctx, null);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<CleanCodePower>(), "CleanCodePotion applied CleanCodePower(1)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FreeFunctionPower>(), "CleanCodePotion applied FreeFunctionPower(1)");
        await AutomatonMechanics.EncodeCardAsync(ctx, player, (BronzeCard)combatState.CreateCard<Downfall.Automaton.Cards.Break>(player));
        var cleanFunc = AutoTestAssert.NotNull(await AutomatonMechanics.CompileFunctionAsync(ctx, player), "Compiled Break with CleanCodePower + FreeFunctionPower");
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count(c => c.Type == CardType.Status), "CleanCodePower suppressed Break's 5-Status Compile Error");
        AutoTestAssert.Equal(0, cleanFunc.EnergyCost.GetWithModifiers(CostModifiers.All), "FreeFunctionPower made compiled Function cost 0 until played");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<CleanCodePower>(), "CleanCodePower decremented to 0");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<FreeFunctionPower>(), "FreeFunctionPower decremented to 0");

        // BuildAFunctionPotion (test potency=2 Sacred Bark -> 2 total copies of compiled Function!)
        await ClearHandAndRefillAsync();
        ClearSequence();
        var bafProc = await PotionCmd.TryToProcure<Downfall.Automaton.Potions.BuildAFunctionPotion>(player);
        var bafPotion = AutoTestAssert.NotNull(bafProc.potion, "Procured BuildAFunctionPotion");
        bafPotion.DynamicVars["Copies"].BaseValue = 2m;
        await bafPotion.OnUseWrapper(ctx, null);
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<FunctionCard>().Count(), "BuildAFunctionPotion with Copies=2 added 2 copies of the compiled Function");

        // 5. BronzeIdol + ALL 6 Good Statuses + BurnAndBuffPotion (Vexing Draught)!
        await ClearHandAndRefillAsync();
        ClearSequence();
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);
        await RelicCmd.Obtain<BronzeIdol>(player);

        // BurnAndBuffPotion: +2 Str, +2 Dex, +2 Burns into Discard -> BronzeIdol transforms them into BronzeIgnite!
        int igniteBefore = pcs.DiscardPile.Cards.Count(c => c is BronzeIgnite);
        var bnbProc = await PotionCmd.TryToProcure<Downfall.Automaton.Potions.BurnAndBuffPotion>(player);
        var bnbPotion = AutoTestAssert.NotNull(bnbProc.potion, "Procured BurnAndBuffPotion");
        await bnbPotion.OnUseWrapper(ctx, null);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "BurnAndBuffPotion granted +2 Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "BurnAndBuffPotion granted +2 Dexterity");
        AutoTestAssert.Equal(igniteBefore + 2, pcs.DiscardPile.Cards.Count(c => c is BronzeIgnite), "BronzeIdol converted BurnAndBuffPotion's 2 Burns into BronzeIgnite in Discard Pile");

        // HyperBeam puts 5 Voids on TOP of Draw Pile -> BronzeIdol replaces all 5 IN-PLACE on TOP with BronzeIntoTheVoid!
        var hyper = combatState.CreateCard<HyperBeam>(player);
        await CardPileCmd.Add(hyper, PileType.Hand);
        await PlayCardAsync(hyper);
        AutoTestAssert.Equal(5, pcs.DrawPile.Cards.Take(5).Count(c => c is BronzeIntoTheVoid),
            "BronzeIdol replaced all 5 HyperBeam Voids IN-PLACE on TOP of Draw Pile with BronzeIntoTheVoid");

        // Draw top BronzeIntoTheVoid -> loses 1 Energy, play on enemy -> applies DarkShacklesPower(5)!
        int energyBeforeVoidDraw = pcs.Energy;
        await CardPileCmd.Draw(ctx, 1m, player);
        AutoTestAssert.Equal(energyBeforeVoidDraw - 1, pcs.Energy, "Drawing BronzeIntoTheVoid lost 1 Energy");
        var intoVoid = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeIntoTheVoid>().FirstOrDefault(), "BronzeIntoTheVoid in Hand");
        await PlayCardAsync(intoVoid, enemy);
        AutoTestAssert.Equal(5, enemy.GetPowerAmount<DarkShacklesPower>(), "Playing BronzeIntoTheVoid applied DarkShacklesPower(5)");

        // Insert Dazed -> BronzeDaze (1 Weak), Slimed -> BronzeUsefulSlime (draw 2), Wound -> BronzeGrievousWound (1 Vulnerable), Burn -> BronzeIgnite (10 Soulburn + 2 self-dmg at turn end)!
        await ClearHandAndRefillAsync();
        var dazedGen = combatState.CreateCard<Dazed>(player);
        await CardPileCmd.AddGeneratedCardToCombat(dazedGen, PileType.Hand, player);
        var bronzeDaze = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeDaze>().FirstOrDefault(), "BronzeIdol converted Dazed to BronzeDaze");
        await PlayCardAsync(bronzeDaze, enemy);
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "BronzeDaze applied 1 Weak");

        var woundGen = combatState.CreateCard<Wound>(player);
        await CardPileCmd.AddGeneratedCardToCombat(woundGen, PileType.Hand, player);
        var bronzeWound = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeGrievousWound>().FirstOrDefault(), "BronzeIdol converted Wound to BronzeGrievousWound");
        await PlayCardAsync(bronzeWound, enemy);
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "BronzeGrievousWound applied 1 Vulnerable");

        var slimeGen = combatState.CreateCard<Slimed>(player);
        await CardPileCmd.AddGeneratedCardToCombat(slimeGen, PileType.Hand, player);
        var bronzeSlime = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeUsefulSlime>().FirstOrDefault(), "BronzeIdol converted Slimed to BronzeUsefulSlime");
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<BronzeDefend>(player), PileType.Draw, CardPilePosition.Top);
        int handBeforeSlime = pcs.Hand.Cards.Count;
        await PlayCardAsync(bronzeSlime);
        AutoTestAssert.Equal(handBeforeSlime + 1, pcs.Hand.Cards.Count, "BronzeUsefulSlime exhausted itself and drew 2 cards (net +1)");

        var burnGen = combatState.CreateCard<Burn>(player);
        await CardPileCmd.AddGeneratedCardToCombat(burnGen, PileType.Hand, player);
        var bronzeIgnite = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeIgnite>().FirstOrDefault(), "BronzeIdol converted Burn to BronzeIgnite");
        AutoTestAssert.Equal(CardType.Status, bronzeIgnite.Type, "Good Status preserves CardType.Status");
        await PlayCardAsync(bronzeIgnite, enemy);
        AutoTestAssert.Equal(10, enemy.GetPowerAmount<Downfall.Core.SoulburnPower>(), "BronzeIgnite applied 10 Soulburn");

        // BronzeIdol non-standard Status -> BronzeUnknownStatus (Perplex) preserving Ethereal/Exhaust keywords + FeaturePower draw synergy!
        await ClearHandAndRefillAsync();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await PowerCmd.Remove<DexterityPower>(player.Creature);
        var soot = combatState.CreateCard<Soot>(player);
        soot.AddKeyword(CardKeyword.Ethereal);
        soot.AddKeyword(CardKeyword.Exhaust);
        await CardPileCmd.AddGeneratedCardToCombat(soot, PileType.Hand, player);
        var convertedUnknown = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BronzeUnknownStatus>().FirstOrDefault(),
            "BronzeIdol converted non-standard Status (Soot) into BronzeUnknownStatus (Perplex)");
        AutoTestAssert.Equal(CardType.Status, convertedUnknown.Type, "BronzeUnknownStatus preserves CardType.Status");
        AutoTestAssert.True(convertedUnknown.Keywords.Contains(CardKeyword.Ethereal), "BronzeUnknownStatus preserved Ethereal keyword");
        AutoTestAssert.True(convertedUnknown.Keywords.Contains(CardKeyword.Exhaust), "BronzeUnknownStatus preserved Exhaust keyword");
        CardCmd.Upgrade(convertedUnknown);
        await PlayCardAsync(convertedUnknown);
        AutoTestAssert.Equal(11, player.Creature.Block, "BronzeUnknownStatus+ (Perplex+) granted 11 Block");

        // Verify Good Status triggers FeaturePower on draw + BronzeIgnite.OnTurnEndInHand deals 2 unpowered self-damage at real turn end!
        await ClearHandAndRefillAsync();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await PowerCmd.Apply<FeaturePower>(ctx, player.Creature, 3m, player.Creature, null);
        var igniteForDraw = combatState.CreateCard<BronzeIgnite>(player);
        await CardPileCmd.Add(igniteForDraw, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1m, player);
        AutoTestAssert.Equal(3, player.Creature.Block, "Drawing Good Status (BronzeIgnite) triggered FeaturePower (+3 Block)");

        // Clear Block and end turn with BronzeIgnite in Hand -> takes 2 self-damage (keep enemy stunned/non-lethal by giving enemy 0 strength / checking HP drop >= 2)!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int hpBeforeIgniteTurnEnd = player.Creature.CurrentHp;
        // Remove ProtectiveGoggles so it doesn't absorb BronzeIgnite's 2 self-damage!
        await RelicCmd.Remove(player.Relics.OfType<ProtectiveGoggles>().First());
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.True(player.Creature.CurrentHp <= hpBeforeIgniteTurnEnd - 2,
            $"BronzeIgnite OnTurnEndInHand dealt at least 2 self-damage (HP {hpBeforeIgniteTurnEnd} -> {player.Creature.CurrentHp})");
    }
}
