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

    internal static async Task ClearHandAndRefillEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player, MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState pcs)
    {
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        if (pcs.Energy < 10)
            await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
    }
}
