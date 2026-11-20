using Downfall.Core;
using Downfall.Hermit.Cards;
using Downfall.Hermit.Powers;
using Downfall.Snecko;
using Downfall.Snecko.Cards;
using Downfall.Snecko.Powers;
using Downfall.Snecko.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class SneckoTests
{
    internal static Task TestVisualsRuntime() =>
        DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheSnecko>("SNECKO_VIS01");

    internal static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSnecko>("SNECKO_START01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Snecko PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");
        await CreatureCmd.GainMaxHp(enemy, 300m);

        // 1. Starter stats, deck, and Turn-1 SneckoSoul -> SoulRoll in hand (6 cards total!).
        AutoTestAssert.Equal(85, player.Creature.MaxHp, "Snecko StartingHp is 85");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Snecko starter deck has 10 cards");
        AutoTestAssert.True(player.Relics.OfType<SneckoSoul>().Any(), "Player starts with SneckoSoul");
        var soulRoll = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<SoulRoll>().FirstOrDefault(), "SneckoSoul added SoulRoll to Turn-1 hand");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Turn-1 hand has 5 drawn + 1 SoulRoll = 6 cards");

        // 2. SoulRoll: 0 cost, Retain, Exhaust, gains 3 Block, Muddles a chosen card to a different cost in 0..3.
        var muddleTarget = pcs.Hand.Cards.First(c => c is not SoulRoll);
        int costBefore = muddleTarget.EnergyCost.GetWithModifiers(CostModifiers.All);
        int blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, muddleTarget)))
        {
            AutoTestAssert.True(soulRoll.TryManualPlay(null), "Play SoulRoll");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 3, player.Creature.Block, "SoulRoll granted 3 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(soulRoll), "SoulRoll exhausted");
        int costAfter = muddleTarget.EnergyCost.GetWithModifiers(CostModifiers.All);
        AutoTestAssert.True(costAfter is >= 0 and <= 3 && costAfter != costBefore,
            $"SoulRoll muddled cost from {costBefore} to different value in 0..3 (got {costAfter})");

        // 3. Overflow boundary test on TailWhip: 5 cards in hand -> NO gold glow, NO Weak/Vulnerable!
        await ClearHandAndRefillEnergy(player, pcs);
        var tailWhip5 = combatState.CreateCard<TailWhip>(player);
        await CardPileCmd.Add(tailWhip5, PileType.Hand);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        AutoTestAssert.Equal(5, pcs.Hand.Cards.Count, "Hand has exactly 5 cards");
        AutoTestAssert.True(!tailWhip5.ShouldGlowGold, "TailWhip does NOT glow gold at 5 cards in hand");
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tailWhip5.TryManualPlay(enemy), "Play TailWhip at 5 cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 10, enemy.CurrentHp, "TailWhip dealt 10 damage");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<WeakPower>(), "TailWhip did NOT apply Weak at 5 cards");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<VulnerablePower>(), "TailWhip did NOT apply Vulnerable at 5 cards");

        // 4. Overflow active at 6 cards in hand: TailWhip+ glows gold & applies 2 Weak + 2 Vulnerable!
        await ClearHandAndRefillEnergy(player, pcs);
        var tailWhip6 = combatState.CreateCard<TailWhip>(player);
        CardCmd.Upgrade(tailWhip6);
        await CardPileCmd.Add(tailWhip6, PileType.Hand);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Hand has 6 cards");
        AutoTestAssert.True(tailWhip6.ShouldGlowGold, "TailWhip+ glows gold at 6 cards in hand");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tailWhip6.TryManualPlay(enemy), "Play TailWhip+ at 6 cards (Overflow!)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 12, enemy.CurrentHp, "TailWhip+ dealt 12 damage");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "TailWhip+ Overflow applied 2 Weak");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "TailWhip+ Overflow applied 2 Vulnerable");

        // 5. SnekBite+ deals 9 damage (with Vulnerable -> floor(9 * 1.5) = 13) and muddles 2 cards in hand!
        await ClearHandAndRefillEnergy(player, pcs);
        var snekBite = combatState.CreateCard<SnekBite>(player);
        CardCmd.Upgrade(snekBite);
        var targetA = combatState.CreateCard<SneckoStrike>(player);
        var targetB = combatState.CreateCard<SneckoDefend>(player);
        await CardPileCmd.Add(snekBite, PileType.Hand);
        await CardPileCmd.Add(targetA, PileType.Hand);
        await CardPileCmd.Add(targetB, PileType.Hand);
        int costA0 = targetA.EnergyCost.GetWithModifiers(CostModifiers.All);
        int costB0 = targetB.EnergyCost.GetWithModifiers(CostModifiers.All);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(snekBite.TryManualPlay(enemy), "Play SnekBite+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 13, enemy.CurrentHp, "SnekBite+ dealt 13 damage (9 * 1.5 Vulnerable)");
        int costA1 = targetA.EnergyCost.GetWithModifiers(CostModifiers.All);
        int costB1 = targetB.EnergyCost.GetWithModifiers(CostModifiers.All);
        AutoTestAssert.True(costA1 != costA0 && costA1 is >= 0 and <= 3, $"SnekBite+ muddled card A ({costA0} -> {costA1})");
        AutoTestAssert.True(costB1 != costB0 && costB1 is >= 0 and <= 3, $"SnekBite+ muddled card B ({costB0} -> {costB1})");

        // 6. Verify offclass & debuff card pools have >= 3 distinct cards for all Gift tiers!
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Common).Count >= 3, "Common offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Uncommon).Count >= 3, "Uncommon offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Rare).Count >= 3, "Rare offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Common && SneckoOffclass.IsDebuffCard(c)).Count >= 3, "Common debuff offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Uncommon && SneckoOffclass.IsDebuffCard(c)).Count >= 3, "Uncommon debuff offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, SneckoOffclass.IsDebuffCard).Count >= 3, "Any debuff offclass pool >= 3");

        // 7. Muddled cost expires cleanly at turn end (next turn)!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(1, targetA.EnergyCost.GetWithModifiers(CostModifiers.All), "Muddled cost expired after turn end");
        AutoTestAssert.True(!pcs.Hand.Cards.OfType<SoulRoll>().Any(), "SneckoSoul does not re-add SoulRoll on Turn 2");

        // 8. TouchOfOrobas upgrades SneckoSoul -> SuperSneckoSoul (Starter rarity, Orobas-only).
        AutoTestAssert.Equal(RelicRarity.Starter, ModelDb.Relic<SuperSneckoSoul>().Rarity, "SuperSneckoSoul is Starter rarity");
        var orobas = (MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas)ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas>().ToMutable();
        AutoTestAssert.True(orobas.SetupForPlayer(player), "TouchOfOrobas found SneckoSoul");
        AutoTestAssert.Equal(ModelDb.Relic<SuperSneckoSoul>().Id, orobas.UpgradedRelic, "TouchOfOrobas upgrades SneckoSoul -> SuperSneckoSoul");
        await RelicCmd.Obtain(orobas, player);
        AutoTestAssert.True(player.Relics.OfType<SuperSneckoSoul>().Any(), "TouchOfOrobas granted SuperSneckoSoul");
        AutoTestAssert.True(!player.Relics.OfType<SneckoSoul>().Any(), "TouchOfOrobas replaced SneckoSoul");

        // 9. SuperSneckoSoul alternating turns: Round 3 adds SoulRoll (6 cards), Round 4 draws 1 extra card (6 cards) & muddles it (no SoulRoll)!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        var round3SoulRoll = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<SoulRoll>().FirstOrDefault(), "SuperSneckoSoul added SoulRoll on Round 3");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "SuperSneckoSoul Round 3 hand size = 5 drawn + 1 SoulRoll");
        // Discard SoulRoll so Round 4 doesn't retain it, making Round 4's draw+muddle assertion exact.
        await CardPileCmd.Add(round3SoulRoll, PileType.Exhaust);

        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 4);
        AutoTestAssert.True(!pcs.Hand.Cards.OfType<SoulRoll>().Any(), "SuperSneckoSoul did NOT add SoulRoll on alternate Round 4");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "SuperSneckoSoul drew 1 extra card (6 total) on Round 4");
        AutoTestAssert.True(
            pcs.Hand.Cards.Any(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) != c.EnergyCost.Canonical),
            "SuperSneckoSoul muddled the extra drawn card on Round 4");
    }

    internal static async Task TestCommonCardsInCombat()
    {
        var (player, runState, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSnecko>("SNECKO_COM01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Snecko PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);
        var ctx = new ThrowingPlayerChoiceContext();

        // 1. Gift deck-obtain test: adding Medusa to Deck triggers Gift choice and adds a Common Debuff offclass card!
        int deckBeforeGift = player.Deck.Cards.Count;
        var deckMedusa = (Medusa)runState.CreateCard<Medusa>(player);
        await CardPileCmd.Add(deckMedusa, PileType.Deck);
        AutoTestAssert.True(deckMedusa.GiftClaimed, "Medusa marked GiftClaimed=true after deck obtain");
        AutoTestAssert.Equal(deckBeforeGift + 2, player.Deck.Cards.Count, "Medusa Gift added 1 offclass card to deck alongside Medusa");
        var giftedCard = player.Deck.Cards.Last();
        AutoTestAssert.True(SneckoOffclass.IsOffclass(giftedCard, player), "Medusa Gift card is Offclass");
        AutoTestAssert.Equal(CardRarity.Common, giftedCard.Rarity, "Medusa Gift card is Common");
        AutoTestAssert.True(SneckoOffclass.IsDebuffCard(giftedCard), "Medusa Gift card is a Debuff card");

        // 2. Amass: 9 base Block + total energy cost of Hand (Amass=2 + Strike=1 + TailWhip=2 -> +5 = 14 Block)!
        await ClearHandAndRefillEnergy(player, pcs);
        var amass = combatState.CreateCard<Amass>(player);
        var c1 = combatState.CreateCard<SneckoStrike>(player);
        var c2 = combatState.CreateCard<TailWhip>(player);
        await CardPileCmd.Add(amass, PileType.Hand);
        await CardPileCmd.Add(c1, PileType.Hand);
        await CardPileCmd.Add(c2, PileType.Hand);
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(amass.TryManualPlay(null), "Play Amass");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 14, player.Creature.Block, "Amass gained 9 base + 5 hand cost = 14 Block");

        // 3. Behold (Overflow -> +2 Shivs), DiceBlock (Overflow -> 2x Block), DiceCrush (Overflow -> draw 2), QuickMove (Overflow -> 1 Vuln ALL)!
        await ClearHandAndRefillEnergy(player, pcs);
        var behold = combatState.CreateCard<Behold>(player);
        var diceBlock = combatState.CreateCard<DiceBlock>(player);
        var diceCrush = combatState.CreateCard<DiceCrush>(player);
        var quickMove = combatState.CreateCard<QuickMove>(player);
        await CardPileCmd.Add(behold, PileType.Hand);
        await CardPileCmd.Add(diceBlock, PileType.Hand);
        await CardPileCmd.Add(diceCrush, PileType.Hand);
        await CardPileCmd.Add(quickMove, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);

        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(behold.TryManualPlay(enemy1), "Play Behold with Overflow (6 cards)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "Behold dealt 6 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<Shiv>().Count(), "Behold Overflow added 2 Shivs");

        // Hand now has 7 cards -> Overflow still active for DiceBlock (5+5=10 Block), DiceCrush (18 dmg + draw 2), QuickMove (7 Block + 1 Vuln ALL)!
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(diceBlock.TryManualPlay(null), "Play DiceBlock with Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 10, player.Creature.Block, "DiceBlock Overflow gained 5+5=10 Block");

        await CardPileCmd.Add(combatState.CreateCard<SneckoStrike>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<SneckoStrike>(player), PileType.Draw, CardPilePosition.Top);
        int handBeforeCrush = pcs.Hand.Cards.Count;
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(diceCrush.TryManualPlay(enemy1), "Play DiceCrush with Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 18, enemy1.CurrentHp, "DiceCrush dealt 18 damage");
        AutoTestAssert.Equal(handBeforeCrush - 1 + 2, pcs.Hand.Cards.Count, "DiceCrush Overflow drew 2 cards");

        blockBefore = player.Creature.Block;
        AutoTestAssert.True(quickMove.TryManualPlay(null), "Play QuickMove with Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 7, player.Creature.Block, "QuickMove gained 7 Block");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<VulnerablePower>(), "QuickMove Overflow applied 1 Vulnerable to enemy 1");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<VulnerablePower>(), "QuickMove Overflow applied 1 Vulnerable to enemy 2");
        await PowerCmd.Remove<VulnerablePower>(enemy1);
        await PowerCmd.Remove<VulnerablePower>(enemy2);

        // 4. BeyondArmor (5 Block + pull 2 offclass from Draw), Cower+ (18 Block + HoleUp+ + Exhaust), CrystalBoomerang (pull offclass from Discard -> +5 Block)!
        await ClearHandAndRefillEnergy(player, pcs);
        var drawOff1 = combatState.CreateCard<Bash>(player);
        var drawOff2 = combatState.CreateCard<Neutralize>(player);
        await CardPileCmd.Add(drawOff1, PileType.Draw);
        await CardPileCmd.Add(drawOff2, PileType.Draw);
        var beyondArmor = combatState.CreateCard<BeyondArmor>(player);
        await CardPileCmd.Add(beyondArmor, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(beyondArmor.TryManualPlay(null), "Play BeyondArmor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 5, player.Creature.Block, "BeyondArmor gained 5 Block");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawOff1) && pcs.Hand.Cards.Contains(drawOff2), "BeyondArmor fetched 2 offclass cards from Draw");

        var cower = combatState.CreateCard<Cower>(player);
        CardCmd.Upgrade(cower);
        await CardPileCmd.Add(cower, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(cower.TryManualPlay(null), "Play Cower+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 18, player.Creature.Block, "Cower+ gained 18 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(cower), "Cower+ exhausted");
        var holeUp = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<HoleUp>().FirstOrDefault(), "Cower+ added HoleUp to Hand");
        AutoTestAssert.True(holeUp.IsUpgraded, "Cower+ added Upgraded HoleUp+");

        var discardOff = combatState.CreateCard<ShrugItOff>(player);
        await CardPileCmd.Add(discardOff, PileType.Discard);
        var boomerang = combatState.CreateCard<CrystalBoomerang>(player);
        await CardPileCmd.Add(boomerang, PileType.Hand);
        blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, discardOff)))
        {
            AutoTestAssert.True(boomerang.TryManualPlay(null), "Play CrystalBoomerang picking offclass card");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(discardOff), "CrystalBoomerang returned card to Hand");
        AutoTestAssert.Equal(blockBefore + 5, player.Creature.Block, "CrystalBoomerang granted 5 Block for Offclass card");

        // 5. Medusa (7 dmg + 2 Venom) -> IronFang (6 dmg + 1 Weak ALL -> triggers Venom for 2 HP loss!) + Negative Strength triggers Venom, Venom itself does NOT!
        await ClearHandAndRefillEnergy(player, pcs);
        var medusa = combatState.CreateCard<Medusa>(player);
        await CardPileCmd.Add(medusa, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(medusa.TryManualPlay(enemy1), "Play Medusa");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "Medusa dealt 7 damage (and applying Venom did not self-trigger)");
        AutoTestAssert.Equal(2, enemy1.GetPowerAmount<VenomPower>(), "Medusa applied 2 Venom");

        var ironFang = combatState.CreateCard<IronFang>(player);
        await CardPileCmd.Add(ironFang, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(ironFang.TryManualPlay(null), "Play IronFang");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6 - 2, enemy1.CurrentHp, "IronFang dealt 6 dmg + Weak triggered Venom for 2 HP loss on enemy 1");
        AutoTestAssert.Equal(e2Hp - 6, enemy2.CurrentHp, "IronFang dealt 6 dmg to enemy 2");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "IronFang applied 1 Weak to enemy 1");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<WeakPower>(), "IronFang applied 1 Weak to enemy 2");

        // Negative Strength debuff from player triggers Venom; self-debuff does NOT!
        e1Hp = enemy1.CurrentHp;
        await PowerCmd.Apply<StrengthPower>(ctx, enemy1, -1m, player.Creature, null);
        AutoTestAssert.Equal(e1Hp - 2, enemy1.CurrentHp, "Negative Strength from player triggered Venom (2 HP loss)");
        e1Hp = enemy1.CurrentHp;
        await PowerCmd.Apply<WeakPower>(ctx, enemy1, 1m, enemy1, null);
        AutoTestAssert.Equal(e1Hp, enemy1.CurrentHp, "Enemy self-debuff did NOT trigger Venom");
        await PowerCmd.Remove<VenomPower>(enemy1);

        // 6. LaserEyes (3-cost: 15 dmg + 1 Energy) triggers LilGuardian in Hand (auto-plays for 7 Block)!
        await ClearHandAndRefillEnergy(player, pcs);
        var laserEyes = combatState.CreateCard<LaserEyes>(player);
        var lilGuardian = combatState.CreateCard<LilGuardian>(player);
        await CardPileCmd.Add(laserEyes, PileType.Hand);
        await CardPileCmd.Add(lilGuardian, PileType.Hand);
        int energyBefore = pcs.Energy;
        blockBefore = player.Creature.Block;
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(laserEyes.TryManualPlay(enemy1), "Play LaserEyes (3-cost)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 15, enemy1.CurrentHp, "LaserEyes dealt 15 damage");
        AutoTestAssert.Equal(energyBefore - 3 + 1, pcs.Energy, "LaserEyes cost 3 and refunded 1 Energy");
        AutoTestAssert.Equal(blockBefore + 7, player.Creature.Block, "LilGuardian auto-played from Hand on 3-cost card for 7 Block");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(lilGuardian), "LilGuardian moved to Discard after auto-play");

        // 7. Nope (5 Block + Exhaust offclass Ironclad Bash -> adds random Ironclad card to Hand)!
        await ClearHandAndRefillEnergy(player, pcs);
        var nope = combatState.CreateCard<Nope>(player);
        var ironcladCard = combatState.CreateCard<Bash>(player);
        await CardPileCmd.Add(nope, PileType.Hand);
        await CardPileCmd.Add(ironcladCard, PileType.Hand);
        blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, ironcladCard)))
        {
            AutoTestAssert.True(nope.TryManualPlay(null), "Play Nope exhausting Bash");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 5, player.Creature.Block, "Nope gained 5 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(ironcladCard), "Nope exhausted Bash");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "Nope added 1 replacement card to Hand");
        AutoTestAssert.Equal(ironcladCard.Pool, pcs.Hand.Cards[0].Pool, "Nope replacement card is from same class pool");

        // 8. OtherworldlySlash (1 hit before offclass played, 2 hits + gold glow after offclass played)!
        await ClearHandAndRefillEnergy(player, pcs);
        var slash1 = combatState.CreateCard<OtherworldlySlash>(player);
        var offPlay = combatState.CreateCard<Neutralize>(player);
        var slash2 = combatState.CreateCard<OtherworldlySlash>(player);
        await CardPileCmd.Add(slash1, PileType.Hand);
        await CardPileCmd.Add(offPlay, PileType.Hand);
        await CardPileCmd.Add(slash2, PileType.Hand);
        AutoTestAssert.True(!slash1.ShouldGlowGold, "OtherworldlySlash does NOT glow gold before offclass played");
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(slash1.TryManualPlay(enemy1), "Play OtherworldlySlash #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "OtherworldlySlash #1 dealt 7 damage (1 hit)");

        AutoTestAssert.True(offPlay.TryManualPlay(enemy1), "Play Neutralize (Offclass)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(slash2.ShouldGlowGold, "OtherworldlySlash glows gold after offclass played");
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(slash2.TryManualPlay(enemy1), "Play OtherworldlySlash #2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 14, enemy1.CurrentHp, "OtherworldlySlash #2 dealt 14 damage (2 hits)");

        // 9. PowerShot (0-cost 5 dmg; returns from Discard when Power played), QuickBite (7 dmg + draw 1 & muddle), SaveForLater (8 dmg + CoalescencePower), WideSting (7 dmg ALL + upgrades offclass in Hand)!
        await ClearHandAndRefillEnergy(player, pcs);
        var powerShot = combatState.CreateCard<PowerShot>(player);
        var inflame = combatState.CreateCard<Inflame>(player);
        await CardPileCmd.Add(powerShot, PileType.Hand);
        await CardPileCmd.Add(inflame, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(powerShot.TryManualPlay(enemy1), "Play PowerShot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 5, enemy1.CurrentHp, "PowerShot dealt 5 damage");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(powerShot), "PowerShot in Discard");
        AutoTestAssert.True(inflame.TryManualPlay(null), "Play Inflame (Power)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(powerShot), "PowerShot returned to Hand on Power play");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        var topDraw = combatState.CreateCard<SneckoDefend>(player);
        await CardPileCmd.Add(topDraw, PileType.Draw, CardPilePosition.Top);
        var quickBite = combatState.CreateCard<QuickBite>(player);
        await CardPileCmd.Add(quickBite, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(quickBite.TryManualPlay(enemy1), "Play QuickBite");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "QuickBite dealt 7 damage");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(topDraw), "QuickBite drew top card");
        AutoTestAssert.True(topDraw.EnergyCost.GetWithModifiers(CostModifiers.All) != 1, "QuickBite muddled drawn card");

        var saveForLater = combatState.CreateCard<SaveForLater>(player);
        await CardPileCmd.Add(saveForLater, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(saveForLater.TryManualPlay(enemy1), "Play SaveForLater");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 8, enemy1.CurrentHp, "SaveForLater dealt 8 damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<CoalescencePower>(), "SaveForLater applied 1 CoalescencePower");

        await ClearHandAndRefillEnergy(player, pcs);
        var wideSting = combatState.CreateCard<WideSting>(player);
        var offToUpg = combatState.CreateCard<Bash>(player);
        var ownNoUpg = combatState.CreateCard<SneckoStrike>(player);
        await CardPileCmd.Add(wideSting, PileType.Hand);
        await CardPileCmd.Add(offToUpg, PileType.Hand);
        await CardPileCmd.Add(ownNoUpg, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(wideSting.TryManualPlay(null), "Play WideSting");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "WideSting dealt 7 damage to enemy 1");
        AutoTestAssert.Equal(e2Hp - 7, enemy2.CurrentHp, "WideSting dealt 7 damage to enemy 2");
        AutoTestAssert.True(offToUpg.IsUpgraded, "WideSting upgraded Offclass card in Hand");
        AutoTestAssert.True(!ownNoUpg.IsUpgraded, "WideSting did NOT upgrade Snecko card in Hand");
    }

    internal static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSnecko>("SNECKO_UNC1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Snecko PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);
        var ctx = new ThrowingPlayerChoiceContext();

        // 0. Validate TokenCardPool ViperEssence portrait/loc/hovertips explicitly.
        var canonicalViper = ModelDb.Card<ViperEssence>();
        AutoTestAssert.True(Godot.ResourceLoader.Exists(canonicalViper.PortraitPath), "ViperEssence portrait exists");
        AutoTestAssert.True(Godot.ResourceLoader.Exists(canonicalViper.CustomPortraitPath), "ViperEssence big portrait exists");
        AutoTestAssert.True(canonicalViper.Description.Exists(), "ViperEssence description exists");
        var viperDesc = canonicalViper.GetDescriptionForPile(PileType.None);
        var viperUpgDesc = canonicalViper.GetDescriptionForUpgradePreview();
        AutoTestAssert.True(!string.IsNullOrWhiteSpace(viperDesc) && !viperDesc.Contains('{') && !viperDesc.Contains('}'), $"Valid ViperEssence desc: {viperDesc}");
        AutoTestAssert.True(!string.IsNullOrWhiteSpace(viperUpgDesc) && !viperUpgDesc.Contains('{') && !viperUpgDesc.Contains('}'), $"Valid ViperEssence+ desc: {viperUpgDesc}");
        _ = canonicalViper.HoverTips.ToList();

        // 1. BlunderGuard (base + upgraded stack -> 14 Block + 5 Strength on 3+ cost card) + DragonsHoard (3 Str + 3 Dex, 3-cost triggers BlunderGuard!).
        await ClearHandAndRefillEnergy(player, pcs);
        var bg1 = combatState.CreateCard<BlunderGuard>(player);
        var bg2 = combatState.CreateCard<BlunderGuard>(player);
        CardCmd.Upgrade(bg2);
        var hoard = combatState.CreateCard<DragonsHoard>(player);
        await CardPileCmd.Add(bg1, PileType.Hand);
        await CardPileCmd.Add(bg2, PileType.Hand);
        await CardPileCmd.Add(hoard, PileType.Hand);
        AutoTestAssert.True(bg1.TryManualPlay(null), "Play BlunderGuard");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(bg2.TryManualPlay(null), "Play BlunderGuard+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(14, player.Creature.GetPowerAmount<BigGunsBlockPower>(), "BigGunsBlockPower stacked 6+8=14");
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<BigGunsStrengthPower>(), "BigGunsStrengthPower stacked 2+3=5");

        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(hoard.TryManualPlay(null), "Play DragonsHoard (3-cost)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 14, player.Creature.Block, "BlunderGuard triggered on 3-cost DragonsHoard (+14 Block)");
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<StrengthPower>(), "DragonsHoard (+3) + BigGunsStrength (+5) = 8 Strength");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<DexterityPower>(), "DragonsHoard granted +3 Dexterity");
        foreach (var p in player.Creature.Powers.ToList())
            await PowerCmd.Remove(p);

        // 2. Belittle (9 HP loss per debuff, including negative Strength!) + MakeshiftBlade (9 dmg + draw 3 if >= 3 debuffs)!
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Apply<WeakPower>(ctx, enemy1, 2m, player.Creature, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, enemy1, 2m, player.Creature, null);
        await PowerCmd.Apply<StrengthPower>(ctx, enemy1, -2m, player.Creature, null);
        var belittle = combatState.CreateCard<Belittle>(player);
        await CardPileCmd.Add(belittle, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(belittle.TryManualPlay(enemy1), "Play Belittle against 3 debuffs");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 27, enemy1.CurrentHp, "Belittle dealt 9*3=27 HP loss");

        for (int i = 0; i < 3; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        var makeshift = combatState.CreateCard<MakeshiftBlade>(player);
        await CardPileCmd.Add(makeshift, PileType.Hand);
        int handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(makeshift.TryManualPlay(enemy1), "Play MakeshiftBlade with 3 debuffs");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBefore - 1 + 3, pcs.Hand.Cards.Count, "MakeshiftBlade drew 3 cards at >= 3 debuffs");
        foreach (var p in enemy1.Powers.ToList())
            await PowerCmd.Remove(p);

        // 3. LatchOn (7 dmg ALL + adds ViperEssence Offclass Token) -> ViperEssence (+1 Str, counts as Offclass!) -> ComboString (hits once for ViperEssence) + DefensiveFlair (8 + 2*offclass in Hand)!
        await ClearHandAndRefillEnergy(player, pcs);
        var latchOn = combatState.CreateCard<LatchOn>(player);
        await CardPileCmd.Add(latchOn, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(latchOn.TryManualPlay(null), "Play LatchOn");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "LatchOn dealt 7 to enemy 1");
        AutoTestAssert.Equal(e2Hp - 7, enemy2.CurrentHp, "LatchOn dealt 7 to enemy 2");
        var viper = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<ViperEssence>().FirstOrDefault(), "LatchOn added ViperEssence");
        AutoTestAssert.True(SneckoOffclass.IsOffclass(viper, player), "ViperEssence counts as Offclass");
        AutoTestAssert.True(viper.TryManualPlay(null), "Play ViperEssence");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "ViperEssence granted 1 Strength");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        var offBash = combatState.CreateCard<Bash>(player);
        var comboString = combatState.CreateCard<ComboString>(player);
        var defFlair = combatState.CreateCard<DefensiveFlair>(player);
        await CardPileCmd.Add(offBash, PileType.Hand);
        await CardPileCmd.Add(comboString, PileType.Hand);
        await CardPileCmd.Add(defFlair, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(defFlair.TryManualPlay(null), "Play DefensiveFlair with 1 Offclass card in Hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 10, player.Creature.Block, "DefensiveFlair gained 8 + 2*1 = 10 Block");

        // Now play Bash (2nd offclass this turn -> ViperEssence + Bash = 2 offclass played, plus Bash applies 2 Vuln!)
        AutoTestAssert.True(offBash.TryManualPlay(enemy1), "Play Bash (2nd offclass)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        await PowerCmd.Remove<VulnerablePower>(enemy1);
        var comboHandDesc = comboString.GetDescriptionForPile(PileType.Hand);
        AutoTestAssert.True(!string.IsNullOrWhiteSpace(comboHandDesc) && !comboHandDesc.Contains('{') && !comboHandDesc.Contains('}'), $"Valid ComboString InCombat desc: {comboHandDesc}");
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(comboString.TryManualPlay(enemy1), "Play ComboString (2 offclass played this turn)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 14, enemy1.CurrentHp, "ComboString hit twice for 7*2=14 damage");

        // 4. Deception (11 Block + Shockwave + Exhaust), DiceBoulder (0->1 cost, 7->11 Block), GildedShield (8 Block, muddles self & returns to Hand), Jackpot+ (Retain, Exhaust, +3 Energy)!
        await ClearHandAndRefillEnergy(player, pcs);
        var deception = combatState.CreateCard<Deception>(player);
        await CardPileCmd.Add(deception, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(deception.TryManualPlay(null), "Play Deception");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 11, player.Creature.Block, "Deception gained 11 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(deception), "Deception exhausted");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<Shockwave>().Any(), "Deception added Shockwave to Hand");

        var boulder = combatState.CreateCard<DiceBoulder>(player);
        await CardPileCmd.Add(boulder, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(boulder.TryManualPlay(null), "Play DiceBoulder #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 7, player.Creature.Block, "DiceBoulder #1 gained 7 Block");
        AutoTestAssert.Equal(11m, boulder.DynamicVars.Block.BaseValue, "DiceBoulder Block increased 7 -> 11");
        AutoTestAssert.Equal(1, boulder.EnergyCost.GetWithModifiers(CostModifiers.All), "DiceBoulder cost increased 0 -> 1");

        var gilded = combatState.CreateCard<GildedShield>(player);
        await CardPileCmd.Add(gilded, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(gilded.TryManualPlay(null), "Play GildedShield");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "GildedShield gained 8 Block");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(gilded), "GildedShield returned to Hand");
        AutoTestAssert.True(gilded.EnergyCost.GetWithModifiers(CostModifiers.All) != 1, "GildedShield muddled its own cost");

        var jackpot = combatState.CreateCard<Downfall.Snecko.Cards.Jackpot>(player);
        CardCmd.Upgrade(jackpot);
        await CardPileCmd.Add(jackpot, PileType.Hand);
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(jackpot.TryManualPlay(null), "Play Jackpot+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBefore - 3 + 3, pcs.Energy, "Jackpot+ spent 3 and gained 3 Energy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(jackpot), "Jackpot+ exhausted");

        // 5. Lacerate+ (4 Venom ALL + Haze+ + Exhaust), Mesmerize (-2 Str ALL + Muddle 1 + Exhaust), MarkedCard (muddle 3-cost card strictly cheaper -> 0..2)!
        await ClearHandAndRefillEnergy(player, pcs);
        var lacerate = combatState.CreateCard<Lacerate>(player);
        CardCmd.Upgrade(lacerate);
        await CardPileCmd.Add(lacerate, PileType.Hand);
        AutoTestAssert.True(lacerate.TryManualPlay(null), "Play Lacerate+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, enemy1.GetPowerAmount<VenomPower>(), "Lacerate+ applied 4 Venom to enemy 1");
        AutoTestAssert.Equal(4, enemy2.GetPowerAmount<VenomPower>(), "Lacerate+ applied 4 Venom to enemy 2");
        var haze = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<Haze>().FirstOrDefault(), "Lacerate+ added Haze");
        AutoTestAssert.True(haze.IsUpgraded, "Lacerate+ added Upgraded Haze+");
        await PowerCmd.Remove<VenomPower>(enemy1);
        await PowerCmd.Remove<VenomPower>(enemy2);

        var mesmerize = combatState.CreateCard<Mesmerize>(player);
        var cost3Card = combatState.CreateCard<LaserEyes>(player);
        await CardPileCmd.Add(mesmerize, PileType.Hand);
        await CardPileCmd.Add(cost3Card, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, cost3Card)))
        {
            AutoTestAssert.True(mesmerize.TryManualPlay(null), "Play Mesmerize");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(-2, enemy1.GetPowerAmount<StrengthPower>(), "Mesmerize reduced enemy 1 Strength by 2");
        AutoTestAssert.Equal(-2, enemy2.GetPowerAmount<StrengthPower>(), "Mesmerize reduced enemy 2 Strength by 2");
        AutoTestAssert.True(cost3Card.EnergyCost.GetWithModifiers(CostModifiers.All) != 3, "Mesmerize muddled LaserEyes");

        var marked = combatState.CreateCard<MarkedCard>(player);
        var expensive = combatState.CreateCard<LaserEyes>(player);
        await CardPileCmd.Add(marked, PileType.Hand);
        await CardPileCmd.Add(expensive, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, expensive)))
        {
            AutoTestAssert.True(marked.TryManualPlay(null), "Play MarkedCard on 3-cost LaserEyes");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        int cheaperCost = expensive.EnergyCost.GetWithModifiers(CostModifiers.All);
        AutoTestAssert.True(cheaperCost is >= 0 and <= 2, $"MarkedCard muddled 3-cost strictly cheaper (got {cheaperCost})");

        // 6. LuckyBreak (8 Block + draw 1 per 2+ cost card in Hand) + MintCondition (Overflow -> +3 Strength) + FlashInThePan (13 Block, discard Hand, draw that many next turn) + CheapStock (start of turn muddles highest cost card)!
        await ClearHandAndRefillEnergy(player, pcs);
        var luckyBreak = combatState.CreateCard<LuckyBreak>(player);
        var twoCost1 = combatState.CreateCard<TailWhip>(player);
        var twoCost2 = combatState.CreateCard<DiceCrush>(player);
        await CardPileCmd.Add(luckyBreak, PileType.Hand);
        await CardPileCmd.Add(twoCost1, PileType.Hand);
        await CardPileCmd.Add(twoCost2, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        blockBefore = player.Creature.Block;
        int handBeforeLucky = pcs.Hand.Cards.Count;
        AutoTestAssert.True(luckyBreak.TryManualPlay(null), "Play LuckyBreak with two 2-cost cards in Hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "LuckyBreak gained 8 Block");
        AutoTestAssert.Equal(handBeforeLucky - 1 + 2, pcs.Hand.Cards.Count, "LuckyBreak drew 2 cards");

        await ClearHandAndRefillEnergy(player, pcs);
        var mint = combatState.CreateCard<MintCondition>(player);
        await CardPileCmd.Add(mint, PileType.Hand);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        AutoTestAssert.True(mint.TryManualPlay(null), "Play MintCondition with Overflow (6 cards)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "MintCondition Overflow granted 3 Strength");

        var cheapStock = combatState.CreateCard<CheapStock>(player);
        await CardPileCmd.Add(cheapStock, PileType.Hand);
        AutoTestAssert.True(cheapStock.TryManualPlay(null), "Play CheapStock");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<CheapStockPower>(), "CheapStock applied CheapStockPower(1)");

        // FlashInThePan with 2 other cards in Hand -> 13 Block, discards 2, applies DrawCardsNextTurnPower(2)!
        await ClearHandAndRefillEnergy(player, pcs);
        var flash = combatState.CreateCard<FlashInThePan>(player);
        await CardPileCmd.Add(flash, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(flash.TryManualPlay(null), "Play FlashInThePan");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 13, player.Creature.Block, "FlashInThePan gained 13 Block");
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count, "FlashInThePan discarded remaining Hand");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "FlashInThePan applied DrawCardsNextTurnPower(2)");

        // End Turn 1 -> Round 2 draws 5+2=7 cards AND CheapStock muddles the highest-cost card!
        var highCard = combatState.CreateCard<LaserEyes>(player);
        await CardPileCmd.Add(highCard, PileType.Draw, CardPilePosition.Top);
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(7, pcs.Hand.Cards.Count, "Round 2 drew 5 + 2 (FlashInThePan) = 7 cards");
        AutoTestAssert.True(highCard.EnergyCost.GetWithModifiers(CostModifiers.All) != 3, "CheapStock muddled the 3-cost LaserEyes at start of Round 2");
    }

    internal static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSnecko>("SNECKO_UNC2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Snecko PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

        // 1. OverwhelmingPresence (draw 1 on FIRST offclass played each turn only) + SerpentsNest (7 dmg ALL whenever Power played)!
        await ClearHandAndRefillEnergy(player, pcs);
        var overwhelming = combatState.CreateCard<OverwhelmingPresence>(player);
        var nest = combatState.CreateCard<SerpentsNest>(player);
        await CardPileCmd.Add(overwhelming, PileType.Hand);
        await CardPileCmd.Add(nest, PileType.Hand);
        AutoTestAssert.True(overwhelming.TryManualPlay(null), "Play OverwhelmingPresence");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int e1HpBeforeNest = enemy1.CurrentHp;
        int e2HpBeforeNest = enemy2.CurrentHp;
        AutoTestAssert.True(nest.TryManualPlay(null), "Play SerpentsNest");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<SerpentsNestPower>(), "SerpentsNestPower(7)");
        AutoTestAssert.Equal(e1HpBeforeNest, enemy1.CurrentHp, "SerpentsNest did NOT self-trigger on enemy 1");
        AutoTestAssert.Equal(e2HpBeforeNest, enemy2.CurrentHp, "SerpentsNest did NOT self-trigger on enemy 2");

        // Play Offclass Power (Inflame) -> triggers SerpentsNest (7 dmg ALL) AND OverwhelmingPresence (+1 draw)!
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        var offPower = combatState.CreateCard<Inflame>(player);
        var offSecond = combatState.CreateCard<Neutralize>(player);
        await CardPileCmd.Add(offPower, PileType.Hand);
        await CardPileCmd.Add(offSecond, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        int handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(offPower.TryManualPlay(null), "Play Inflame (Offclass Power)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "SerpentsNest dealt 7 damage to enemy 1");
        AutoTestAssert.Equal(e2Hp - 7, enemy2.CurrentHp, "SerpentsNest dealt 7 damage to enemy 2");
        AutoTestAssert.Equal(handBefore - 1 + 1, pcs.Hand.Cards.Count, "OverwhelmingPresence drew 1 card on 1st offclass play");

        // 2nd offclass play this turn must NOT draw again!
        handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(offSecond.TryManualPlay(enemy1), "Play Neutralize (2nd offclass play)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBefore - 1, pcs.Hand.Cards.Count, "OverwhelmingPresence did NOT trigger a 2nd time this turn");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<SerpentsNestPower>(player.Creature);
        await PowerCmd.Remove<OverwhelmingPresencePower>(player.Creature);
        await PowerCmd.Remove<WeakPower>(enemy1);

        // 2. PureSnecko (draw 3 & muddle them) + RiskySword (gains +8 dmg when muddled) + RainOfDice (6 dmg, muddle 1, Exhaust, add copy to Hand)!
        await ClearHandAndRefillEnergy(player, pcs);
        var d1 = combatState.CreateCard<SneckoStrike>(player);
        var d2 = combatState.CreateCard<SneckoDefend>(player);
        var d3 = combatState.CreateCard<RiskySword>(player);
        await CardPileCmd.Add(d1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(d2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(d3, PileType.Draw, CardPilePosition.Top);
        var pureSnecko = combatState.CreateCard<PureSnecko>(player);
        await CardPileCmd.Add(pureSnecko, PileType.Hand);
        AutoTestAssert.True(pureSnecko.TryManualPlay(null), "Play PureSnecko");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(d1) && pcs.Hand.Cards.Contains(d2) && pcs.Hand.Cards.Contains(d3), "PureSnecko drew 3 cards");
        AutoTestAssert.True(d1.EnergyCost.GetWithModifiers(CostModifiers.All) != 1, "PureSnecko muddled d1");
        AutoTestAssert.Equal(16m, d3.DynamicVars.Damage.BaseValue, "RiskySword increased damage 8 -> 16 when muddled");

        var rain = combatState.CreateCard<RainOfDice>(player);
        CardCmd.Upgrade(rain);
        await CardPileCmd.Add(rain, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, d3)))
        {
            AutoTestAssert.True(rain.TryManualPlay(enemy1), "Play RainOfDice+ muddling RiskySword again");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(e1Hp - 8, enemy1.CurrentHp, "RainOfDice+ dealt 8 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(rain), "RainOfDice+ exhausted");
        AutoTestAssert.Equal(24m, d3.DynamicVars.Damage.BaseValue, "RiskySword increased damage 16 -> 24 on 2nd muddle");
        var rainCopy = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<RainOfDice>().FirstOrDefault(), "RainOfDice+ added copy to Hand");
        AutoTestAssert.True(rainCopy.IsUpgraded, "RainOfDice+ copy is Upgraded");

        // 3. Tsunami (4 Block per Overflow) + Serpentscale (no Overflow -> 1 Plating; with Overflow -> 3 Plating + 4 Block from Tsunami)!
        await ClearHandAndRefillEnergy(player, pcs);
        var tsunami = combatState.CreateCard<Tsunami>(player);
        var scale1 = combatState.CreateCard<Serpentscale>(player);
        await CardPileCmd.Add(tsunami, PileType.Hand);
        await CardPileCmd.Add(scale1, PileType.Hand);
        AutoTestAssert.True(tsunami.TryManualPlay(null), "Play Tsunami");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(scale1.TryManualPlay(enemy1), "Play Serpentscale without Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PlatingPower>(), "Serpentscale without Overflow granted 1 Plating");
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "Tsunami did NOT trigger without Overflow");

        await ClearHandAndRefillEnergy(player, pcs);
        var scale2 = combatState.CreateCard<Serpentscale>(player);
        await CardPileCmd.Add(scale2, PileType.Hand);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(scale2.TryManualPlay(enemy1), "Play Serpentscale with Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<PlatingPower>(), "Serpentscale Overflow granted 3 Plating (1+3=4)");
        AutoTestAssert.Equal(blockBefore + 4, player.Creature.Block, "Tsunami granted +4 Block on Overflow");
        await PowerCmd.Remove<TsunamiPower>(player.Creature);
        await PowerCmd.Remove<PlatingPower>(player.Creature);

        // 4. SlitherThrough (14 dmg, reduce cost of Offclass cards in Hand by 1 this turn) + SerpentIdol (choose 1 of 3 offclass, costs 0 this turn) + SoulDraw (add 2 random offclass with Retain)!
        await ClearHandAndRefillEnergy(player, pcs);
        var slither = combatState.CreateCard<SlitherThrough>(player);
        var off2Cost = combatState.CreateCard<Bash>(player);
        var own2Cost = combatState.CreateCard<TailWhip>(player);
        await CardPileCmd.Add(slither, PileType.Hand);
        await CardPileCmd.Add(off2Cost, PileType.Hand);
        await CardPileCmd.Add(own2Cost, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(slither.TryManualPlay(enemy1), "Play SlitherThrough");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 14, enemy1.CurrentHp, "SlitherThrough dealt 14 damage");
        AutoTestAssert.Equal(1, off2Cost.EnergyCost.GetWithModifiers(CostModifiers.All), "SlitherThrough reduced Offclass Bash cost 2 -> 1");
        AutoTestAssert.Equal(2, own2Cost.EnergyCost.GetWithModifiers(CostModifiers.All), "SlitherThrough did NOT reduce Snecko TailWhip cost");

        await ClearHandAndRefillEnergy(player, pcs);
        var idol = combatState.CreateCard<SerpentIdol>(player);
        await CardPileCmd.Add(idol, PileType.Hand);
        AutoTestAssert.True(idol.TryManualPlay(null), "Play SerpentIdol");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "SerpentIdol added 1 chosen Offclass card to Hand");
        AutoTestAssert.True(SneckoOffclass.IsOffclass(pcs.Hand.Cards[0], player), "SerpentIdol card is Offclass");
        if (!pcs.Hand.Cards[0].EnergyCost.CostsX)
            AutoTestAssert.Equal(0, pcs.Hand.Cards[0].EnergyCost.GetWithModifiers(CostModifiers.All), "SerpentIdol card costs 0 this turn");

        await ClearHandAndRefillEnergy(player, pcs);
        var soulDraw = combatState.CreateCard<SoulDraw>(player);
        await CardPileCmd.Add(soulDraw, PileType.Hand);
        AutoTestAssert.True(soulDraw.TryManualPlay(null), "Play SoulDraw");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "SoulDraw added 2 Offclass cards");
        AutoTestAssert.True(pcs.Hand.Cards.All(c => SneckoOffclass.IsOffclass(c, player) && c.Keywords.Contains(CardKeyword.Retain)), "SoulDraw cards are Offclass with Retain");

        // 5. ThrowingCards+ (X=2 -> 2 FTL+ in Hand + 1 Energy) + ToothAndClaw+ (6 dmg + Upgraded Shiv per unique Suit in Hand) + Whack+ (12 dmg + Dash+ + Exhaust) + WideAngle (18 dmg ALL, Retain)!
        await ClearHandAndRefillEnergy(player, pcs);
        if (pcs.Energy > 2)
            await PlayerCmd.LoseEnergy(pcs.Energy - 2, player);
        var throwing = combatState.CreateCard<ThrowingCards>(player);
        CardCmd.Upgrade(throwing);
        await CardPileCmd.Add(throwing, PileType.Hand);
        AutoTestAssert.True(throwing.TryManualPlay(null), "Play ThrowingCards+ at X=2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Energy, "ThrowingCards+ refunded 1 Energy after spending X=2");
        var ftls = pcs.Hand.Cards.OfType<Ftl>().ToList();
        AutoTestAssert.Equal(2, ftls.Count, "ThrowingCards+ added 2 FTLs");
        AutoTestAssert.True(ftls.All(c => c.IsUpgraded), "ThrowingCards+ added Upgraded FTL+");

        await ClearHandAndRefillEnergy(player, pcs);
        var tooth = combatState.CreateCard<ToothAndClaw>(player);
        CardCmd.Upgrade(tooth);
        await CardPileCmd.Add(tooth, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<Bash>(player), PileType.Hand); // Ironclad
        await CardPileCmd.Add(combatState.CreateCard<Neutralize>(player), PileType.Hand); // Silent
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(tooth.TryManualPlay(enemy1), "Play ToothAndClaw+ with 3 Suits (Snecko, Ironclad, Silent)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "ToothAndClaw+ dealt 6 damage");
        var shivs = pcs.Hand.Cards.OfType<Shiv>().ToList();
        AutoTestAssert.Equal(3, shivs.Count, "ToothAndClaw+ added 3 Shivs for 3 unique Suits");
        AutoTestAssert.True(shivs.All(s => s.IsUpgraded), "ToothAndClaw+ added Upgraded Shivs");

        await ClearHandAndRefillEnergy(player, pcs);
        var whack = combatState.CreateCard<Whack>(player);
        CardCmd.Upgrade(whack);
        await CardPileCmd.Add(whack, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(whack.TryManualPlay(enemy1), "Play Whack+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 12, enemy1.CurrentHp, "Whack+ dealt 12 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(whack), "Whack+ exhausted");
        var dash = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<Dash>().FirstOrDefault(), "Whack+ added Dash");
        AutoTestAssert.True(dash.IsUpgraded, "Whack+ added Upgraded Dash+");

        var wideAngle = combatState.CreateCard<WideAngle>(player);
        await CardPileCmd.Add(wideAngle, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(wideAngle.TryManualPlay(null), "Play WideAngle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 18, enemy1.CurrentHp, "WideAngle dealt 18 to enemy 1");
        AutoTestAssert.Equal(e2Hp - 18, enemy2.CurrentHp, "WideAngle dealt 18 to enemy 2");

        // 6. RoundaboutSwing (8 dmg, put card on top of Draw, +2 draw next turn) + TrashCan (end of turn exhaust up to 1) + UnendingSupply (start of turn Offclass Echo)!
        await ClearHandAndRefillEnergy(player, pcs);
        var trashCan = combatState.CreateCard<TrashCan>(player);
        var unending = combatState.CreateCard<UnendingSupply>(player);
        var roundabout = combatState.CreateCard<RoundaboutSwing>(player);
        var topTarget = combatState.CreateCard<LaserEyes>(player);
        var trashTarget = combatState.CreateCard<SneckoStrike>(player);
        await CardPileCmd.Add(trashCan, PileType.Hand);
        await CardPileCmd.Add(unending, PileType.Hand);
        await CardPileCmd.Add(roundabout, PileType.Hand);
        await CardPileCmd.Add(topTarget, PileType.Hand);
        await CardPileCmd.Add(trashTarget, PileType.Hand);
        AutoTestAssert.True(trashCan.TryManualPlay(null), "Play TrashCan");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(unending.TryManualPlay(null), "Play UnendingSupply");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topTarget)))
        {
            AutoTestAssert.True(roundabout.TryManualPlay(enemy1), "Play RoundaboutSwing putting LaserEyes on top of Draw");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(topTarget, pcs.DrawPile.Cards.FirstOrDefault(), "RoundaboutSwing placed LaserEyes on top of Draw");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "RoundaboutSwing applied DrawCardsNextTurnPower(2)");

        // End Turn 1 -> TrashCan exhausts trashTarget; Round 2 -> UnendingSupply adds 1 Offclass Echo to Hand!
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, trashTarget)))
        {
            await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        }
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(trashTarget), "TrashCan exhausted selected card at end of Turn 1");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => DownfallEcho.IsEcho(c) && SneckoOffclass.IsOffclass(c, player)), "UnendingSupply added Offclass Echo at start of Round 2");
    }

    internal static async Task TestRareCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSnecko>("SNECKO_RARE");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Snecko PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

        // 1. GlitteringGambit (Unplayable, Eternal -> IsRemovable==false, CanBeGeneratedInCombat==false, Gift: +150 Gold + Upgraded Rare offclass in deck)!
        var gambit = (GlitteringGambit)player.RunState.CreateCard<GlitteringGambit>(player);
        AutoTestAssert.True(!gambit.IsRemovable, "GlitteringGambit is Eternal (IsRemovable == false)");
        AutoTestAssert.True(!gambit.CanBeGeneratedInCombat, "GlitteringGambit cannot be generated in combat");
        int goldBefore = player.Gold;
        int deckBefore = player.Deck.Cards.Count;
        await CardPileCmd.Add(gambit, PileType.Deck);
        AutoTestAssert.Equal(goldBefore + 150, player.Gold, "GlitteringGambit Gift granted +150 Gold");
        AutoTestAssert.Equal(deckBefore + 2, player.Deck.Cards.Count, "GlitteringGambit + 1 Gifted Upgraded Rare added to Deck");
        var giftedRare = player.Deck.Cards.Last();
        AutoTestAssert.True(SneckoOffclass.IsOffclass(giftedRare, player) && giftedRare.Rarity == CardRarity.Rare && giftedRare.IsUpgraded,
            "GlitteringGambit Gift added an Upgraded Rare Offclass card");

        // 2. AceOfWands (Queen of Pentacles: +4 Block on debuff) + Mudshield (+2 Block on Muddle) + SerpentineSleuth (+1 Energy on Power, NO self-trigger)!
        await ClearHandAndRefillEnergy(player, pcs);
        var ace = combatState.CreateCard<AceOfWands>(player);
        var mud = combatState.CreateCard<Mudshield>(player);
        var sleuth = combatState.CreateCard<SerpentineSleuth>(player);
        await CardPileCmd.Add(ace, PileType.Hand);
        await CardPileCmd.Add(mud, PileType.Hand);
        await CardPileCmd.Add(sleuth, PileType.Hand);
        int energyBeforeSleuth = pcs.Energy;
        AutoTestAssert.True(sleuth.TryManualPlay(null), "Play SerpentineSleuth (cost 4)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeSleuth - 4, pcs.Energy, "SerpentineSleuth did NOT self-trigger Energy on itself");

        int energyBeforeAce = pcs.Energy;
        AutoTestAssert.True(ace.TryManualPlay(null), "Play AceOfWands (cost 3 -> Sleuth grants +1 Energy)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeAce - 3 + 1, pcs.Energy, "SerpentineSleuth granted +1 Energy when AceOfWands Power played");

        AutoTestAssert.True(mud.TryManualPlay(null), "Play Mudshield");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<AceOfWandsPower>(), "AceOfWandsPower(4)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<MudshieldPower>(), "MudshieldPower(2)");

        // 3. CobraCoil (20 dmg + ConstrictPower 10 -> triggers AceOfWands +4 Block!) + DangerNoodle (14 dmg + muddle 1 -> triggers Mudshield +2 Block!)
        await ClearHandAndRefillEnergy(player, pcs);
        var cobra = combatState.CreateCard<CobraCoil>(player);
        var noodle = combatState.CreateCard<DangerNoodle>(player);
        var dummy = combatState.CreateCard<SneckoDefend>(player);
        await CardPileCmd.Add(cobra, PileType.Hand);
        await CardPileCmd.Add(noodle, PileType.Hand);
        await CardPileCmd.Add(dummy, PileType.Hand);
        int blockBefore = player.Creature.Block;
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(cobra.TryManualPlay(enemy1), "Play CobraCoil");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 20, enemy1.CurrentHp, "CobraCoil dealt 20 damage");
        AutoTestAssert.Equal(10, enemy1.GetPowerAmount<ConstrictPower>(), "CobraCoil applied ConstrictPower(10)");
        AutoTestAssert.Equal(blockBefore + 4, player.Creature.Block, "AceOfWands granted +4 Block on ConstrictPower debuff");

        blockBefore = player.Creature.Block;
        e1Hp = enemy1.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, dummy)))
        {
            AutoTestAssert.True(noodle.TryManualPlay(enemy1), "Play DangerNoodle");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(e1Hp - 14, enemy1.CurrentHp, "DangerNoodle dealt 14 damage");
        AutoTestAssert.Equal(blockBefore + 2, player.Creature.Block, "Mudshield granted +2 Block on Muddle");
        await PowerCmd.Remove<AceOfWandsPower>(player.Creature);
        await PowerCmd.Remove<MudshieldPower>(player.Creature);
        await PowerCmd.Remove<SerpentineSleuthPower>(player.Creature);

        // 4. Blunderbus (8 dmg ALL, repeat for every 3+ cost card in hand -> 2 x 3+-cost cards => 3 total hits = 24 dmg ALL)!
        await ClearHandAndRefillEnergy(player, pcs);
        var blunder = combatState.CreateCard<Blunderbus>(player);
        await CardPileCmd.Add(blunder, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<LaserEyes>(player), PileType.Hand); // 3-cost
        await CardPileCmd.Add(combatState.CreateCard<CobraCoil>(player), PileType.Hand); // 4-cost
        await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand); // 1-cost
        e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(blunder.TryManualPlay(null), "Play Blunderbus with two 3-cost cards in hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 24, enemy1.CurrentHp, "Blunderbus hit enemy 1 3 times (24 dmg)");
        AutoTestAssert.Equal(e2Hp - 24, enemy2.CurrentHp, "Blunderbus hit enemy 2 3 times (24 dmg)");

        // 5. InertBlade (10 dmg; cost 0 -> no bonus; cost 3 -> draw 3, +3 Strength, +1 Energy)!
        await ClearHandAndRefillEnergy(player, pcs);
        var inert0 = combatState.CreateCard<InertBlade>(player);
        inert0.EnergyCost.SetThisCombat(0);
        await CardPileCmd.Add(inert0, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        int strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(inert0.TryManualPlay(enemy1), "Play InertBlade at cost 0");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 10, enemy1.CurrentHp, "InertBlade(0) dealt 10 damage");
        AutoTestAssert.Equal(strBefore, player.Creature.GetPowerAmount<StrengthPower>(), "InertBlade(0) granted 0 Strength");

        await ClearHandAndRefillEnergy(player, pcs);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        var inert3 = combatState.CreateCard<InertBlade>(player);
        inert3.EnergyCost.SetThisCombat(3);
        await CardPileCmd.Add(inert3, PileType.Hand);
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(inert3.TryManualPlay(enemy1), "Play InertBlade at cost 3");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "InertBlade(3) drew 3 cards");
        AutoTestAssert.Equal(strBefore + 3, player.Creature.GetPowerAmount<StrengthPower>(), "InertBlade(3) granted +3 Strength");
        AutoTestAssert.Equal(energyBefore - 3 + 1, pcs.Energy, "InertBlade(3) spent 3 and gained +1 Energy");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 6. LastStrike ("Final Strike": 6 dmg, repeat for each unique Strike played this combat)!
        await ClearHandAndRefillEnergy(player, pcs);
        var strike1 = combatState.CreateCard<SneckoStrike>(player);
        var strike2 = combatState.CreateCard<SneckoStrike>(player);
        var strike3 = combatState.CreateCard<StrikeIronclad>(player);
        await CardPileCmd.Add(strike1, PileType.Hand);
        await CardPileCmd.Add(strike2, PileType.Hand);
        await CardPileCmd.Add(strike3, PileType.Hand);
        AutoTestAssert.True(strike1.TryManualPlay(enemy1), "Play SneckoStrike #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(strike2.TryManualPlay(enemy1), "Play SneckoStrike #2 (duplicate)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(strike3.TryManualPlay(enemy1), "Play StrikeIronclad (2nd unique Strike)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        var lastStrike = combatState.CreateCard<LastStrike>(player);
        await CardPileCmd.Add(lastStrike, PileType.Hand);
        var inCombatDesc = lastStrike.GetDescriptionForPile(PileType.Hand);
        AutoTestAssert.True(!inCombatDesc.Contains('{') && inCombatDesc.Contains("[green]3[/green] hits"),
            $"LastStrike InCombat description shows 3 hits (1 + 2 unique Strikes): {inCombatDesc}");
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(lastStrike.TryManualPlay(enemy1), "Play LastStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 18, enemy1.CurrentHp, "LastStrike hit 3 times (1 base + 2 unique Strikes = 18 dmg)");

        // 7. PoisonParadise (Fountain: Overflow applies 4 Venom to random enemy) + TyphoonFang (12 dmg, Overflow -> TyphoonPower, NO self-trigger) + PerpetualSerpent (20 dmg, Overflow -> +2 Energy + Fountain Venom + Typhoon auto-plays TyphoonFang without infinite loop)!
        await ClearHandAndRefillEnergy(player, pcs);
        var fountain = combatState.CreateCard<PoisonParadise>(player);
        await CardPileCmd.Add(fountain, PileType.Hand);
        AutoTestAssert.True(fountain.TryManualPlay(null), "Play PoisonParadise (Fountain)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<FountainPower>(), "FountainPower(4)");

        var typhoon = combatState.CreateCard<TyphoonFang>(player);
        await CardPileCmd.Add(typhoon, PileType.Hand);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(typhoon.TryManualPlay(enemy1), "Play TyphoonFang with Overflow (6 cards)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 12, enemy1.CurrentHp, "TyphoonFang dealt 12 damage (did NOT self-trigger extra copy)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<TyphoonPower>(), "TyphoonFang Overflow applied TyphoonPower(1)");
        int totalVenom1 = enemy1.GetPowerAmount<VenomPower>() + enemy2.GetPowerAmount<VenomPower>();
        AutoTestAssert.Equal(4, totalVenom1, "Fountain applied 4 Venom on TyphoonFang Overflow");

        // Now play PerpetualSerpent with Overflow -> 20 dmg + 2 Energy + Fountain +4 Venom + TyphoonFang auto-copy (+12 dmg) without infinite loop!
        var perpetual = combatState.CreateCard<PerpetualSerpent>(player);
        await CardPileCmd.Add(perpetual, PileType.Hand);
        while (pcs.Hand.Cards.Count < 6)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        energyBefore = pcs.Energy;
        int totalEnemyHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(perpetual.TryManualPlay(enemy1), "Play PerpetualSerpent with Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBefore - 2 + 2, pcs.Energy, "PerpetualSerpent spent 2 and gained +2 Energy on Overflow");
        AutoTestAssert.Equal(totalEnemyHpBefore - 32, enemy1.CurrentHp + enemy2.CurrentHp, "PerpetualSerpent (20) + Typhoon auto-copy (12) dealt 32 total damage without looping");
        AutoTestAssert.Equal(8, enemy1.GetPowerAmount<VenomPower>() + enemy2.GetPowerAmount<VenomPower>(), "Fountain applied +4 more Venom on PerpetualSerpent Overflow");
        await PowerCmd.Remove<FountainPower>(player.Creature);
        await PowerCmd.Remove<TyphoonPower>(player.Creature);

        // Also test TyphoonFang+ -> TyphoonPlusPower auto-plays 16-dmg TyphoonFang+ copy on Overflow!
        await ClearHandAndRefillEnergy(player, pcs);
        var typhoonPlus = combatState.CreateCard<TyphoonFang>(player);
        CardCmd.Upgrade(typhoonPlus);
        await CardPileCmd.Add(typhoonPlus, PileType.Hand);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        AutoTestAssert.True(typhoonPlus.TryManualPlay(enemy1), "Play TyphoonFang+ with Overflow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<TyphoonPlusPower>(), "TyphoonFang+ Overflow applied TyphoonPlusPower(1)");
        var diceBlockTrig = combatState.CreateCard<DiceBlock>(player);
        await CardPileCmd.Add(diceBlockTrig, PileType.Hand);
        while (pcs.Hand.Cards.Count < 6)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        totalEnemyHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(diceBlockTrig.TryManualPlay(null), "Play DiceBlock with Overflow to trigger TyphoonPlusPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalEnemyHpBefore - 16, enemy1.CurrentHp + enemy2.CurrentHp, "TyphoonPlusPower auto-played 16-dmg TyphoonFang+ copy");
        // Leave TyphoonPlusPower active to verify it expires at end of turn!

        // 8. Restock (discard hand, draw 6, muddle hand, Exhaust) + SerpentBottle+ (draw 2, muddle 2, Exhaust) + SoulExchange (Retain, Exhaust, muddle hand) + TrashToTreasure (9 Block, exhaust 3-cost card -> +3 Energy, Exhaust)!
        await ClearHandAndRefillEnergy(player, pcs);
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Draw, CardPilePosition.Top);
        var restock = combatState.CreateCard<Restock>(player);
        var discardedCard = combatState.CreateCard<SneckoStrike>(player);
        await CardPileCmd.Add(restock, PileType.Hand);
        await CardPileCmd.Add(discardedCard, PileType.Hand);
        AutoTestAssert.True(restock.TryManualPlay(null), "Play Restock");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(restock), "Restock exhausted");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(discardedCard), "Restock discarded prior hand");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Restock drew 6 cards");
        AutoTestAssert.True(pcs.Hand.Cards.All(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) != 1), "Restock muddled all 6 drawn 1-cost cards");

        await ClearHandAndRefillEnergy(player, pcs);
        var b1 = combatState.CreateCard<SneckoDefend>(player);
        var b2 = combatState.CreateCard<SneckoDefend>(player);
        await CardPileCmd.Add(b1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(b2, PileType.Draw, CardPilePosition.Top);
        var bottle = combatState.CreateCard<SerpentBottle>(player);
        CardCmd.Upgrade(bottle);
        await CardPileCmd.Add(bottle, PileType.Hand);
        AutoTestAssert.True(bottle.TryManualPlay(null), "Play SerpentBottle+ (Discount Sale+)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(bottle), "SerpentBottle+ exhausted");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "SerpentBottle+ drew 2 cards");
        AutoTestAssert.True(b1.EnergyCost.GetWithModifiers(CostModifiers.All) != 1 || b2.EnergyCost.GetWithModifiers(CostModifiers.All) != 1,
            "SerpentBottle+ muddled drawn card(s)");

        var soulEx = combatState.CreateCard<SoulExchange>(player);
        var ex1 = combatState.CreateCard<SneckoStrike>(player);
        await CardPileCmd.Add(soulEx, PileType.Hand);
        await CardPileCmd.Add(ex1, PileType.Hand);
        AutoTestAssert.True(soulEx.TryManualPlay(null), "Play SoulExchange");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(soulEx), "SoulExchange exhausted");
        AutoTestAssert.True(ex1.EnergyCost.GetWithModifiers(CostModifiers.All) != 1, "SoulExchange muddled hand");

        await ClearHandAndRefillEnergy(player, pcs);
        var trash = combatState.CreateCard<Downfall.Snecko.Cards.TrashToTreasure>(player);
        var threeCost = combatState.CreateCard<LaserEyes>(player);
        await CardPileCmd.Add(trash, PileType.Hand);
        await CardPileCmd.Add(threeCost, PileType.Hand);
        blockBefore = player.Creature.Block;
        energyBefore = pcs.Energy;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, threeCost)))
        {
            AutoTestAssert.True(trash.TryManualPlay(null), "Play TrashToTreasure exhausting 3-cost LaserEyes");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 9, player.Creature.Block, "TrashToTreasure gained 9 Block");
        AutoTestAssert.Equal(energyBefore - 1 + 3, pcs.Energy, "TrashToTreasure gained +3 Energy from 3-cost exhausted card");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(trash) && pcs.ExhaustPile.Cards.Contains(threeCost), "TrashToTreasure exhausted both cards");

        // 9. SnakeEyes (double-play next Offclass card, NOT Snecko card!) + SerpentMind (Exotic Form: end of turn +1 Strength per unique color played this turn)!
        await ClearHandAndRefillEnergy(player, pcs);
        var snakeEyes = combatState.CreateCard<SnakeEyes>(player);
        var mind = combatState.CreateCard<SerpentMind>(player);
        var ownStrike = combatState.CreateCard<SneckoStrike>(player);
        var offStrike = combatState.CreateCard<StrikeIronclad>(player);
        await CardPileCmd.Add(snakeEyes, PileType.Hand);
        await CardPileCmd.Add(mind, PileType.Hand);
        await CardPileCmd.Add(ownStrike, PileType.Hand);
        await CardPileCmd.Add(offStrike, PileType.Hand);
        AutoTestAssert.True(snakeEyes.TryManualPlay(null), "Play SnakeEyes");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(mind.TryManualPlay(null), "Play SerpentMind");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        // Playing SneckoStrike must NOT consume SnakeEyes!
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(ownStrike.TryManualPlay(enemy1), "Play SneckoStrike (not Offclass)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "SnakeEyes did NOT double SneckoStrike");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<SnakeEyesPower>(), "SnakeEyes charge preserved after Snecko card");

        // Playing StrikeIronclad (Offclass) MUST double-play (6 + 6 = 12 dmg) and consume SnakeEyes!
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(offStrike.TryManualPlay(enemy1), "Play StrikeIronclad (Offclass)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 12, enemy1.CurrentHp, "SnakeEyes double-played Offclass StrikeIronclad (12 dmg)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<SnakeEyesPower>(), "SnakeEyes charge consumed");

        // End Turn 1 -> SerpentMind grants +Strength equal to unique colors played this turn (>= 2: Snecko + Ironclad) AND CobraCoil's ConstrictPower(10) ticks on enemy1!
        int e1HpBeforeEnd = enemy1.CurrentHp;
        int uniqueSuits = SneckoOffclass.CountUniqueSuitsPlayedThisTurn(player);
        AutoTestAssert.True(uniqueSuits >= 2, $"At least 2 unique colors played this turn (got {uniqueSuits})");
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(uniqueSuits, player.Creature.GetPowerAmount<StrengthPower>(), "SerpentMind granted Strength equal to unique colors played");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<TyphoonPlusPower>(), "TyphoonPlusPower expired at end of turn");
        AutoTestAssert.True(enemy1.CurrentHp <= e1HpBeforeEnd - 10, $"CobraCoil ConstrictPower(10) damaged enemy1 at end of enemy turn (was {e1HpBeforeEnd}, now {enemy1.CurrentHp})");
    }

    internal static async Task ClearHandAndRefillEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player, MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState pcs)
    {
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        if (pcs.Energy < 10)
            await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
    }
}
