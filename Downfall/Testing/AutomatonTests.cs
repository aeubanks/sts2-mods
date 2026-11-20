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
}
