using Downfall.Core;
using Downfall.Guardian;
using Downfall.Guardian.Cards;
using Downfall.Guardian.Orbs;
using Downfall.Guardian.Powers;
using Downfall.Guardian.Relics;
using Downfall.Guardian.RestSite;
using Downfall.Slimebound.Powers;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class GuardianTests
{
    internal static async Task TestVisualsRuntime()
    {
        await DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheGuardian>(
            "GUARDIAN_VIS",
            GuardianMechanics.IsValidGuardianSpritePath);
        DownfallAutoTestRunner.CleanupRun();

        // Verify live StasisOrb (DownfallOrbPatch) and DefensiveModePower sprite swap in combat SceneTree!
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGuardian>("GUARDIAN_ORBVIS");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var ctx = new ThrowingPlayerChoiceContext();

        var orbManager = AutoTestAssert.NotNull(
            NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager,
            "Guardian live NOrbManager");

        var cardToStasis = combatState.CreateCard<GuardianStrike>(player);
        await CardPileCmd.Add(cardToStasis, PileType.Hand);
        var stasis = AutoTestAssert.NotNull(
            await GuardianMechanics.PlaceInStasisAsync(ctx, player, cardToStasis),
            "Placed GuardianStrike into StasisOrb");
        AutoTestAssert.Equal(2, stasis.TurnsRemaining, "1-cost card starts with 2 turns in Stasis");

        var orbNodes = new List<NOrb>();
        DownfallAutoTestRunner.CollectNodes(orbManager, orbNodes);
        AutoTestAssert.True(orbNodes.Any(n => ReferenceEquals(n.Model, stasis)), "Live NOrb mounted for StasisOrb");

        // Verify Defensive Mode swaps combat sprite guardian.png -> guardian_defensive.png -> guardian.png!
        var creatures = new List<NCreature>();
        DownfallAutoTestRunner.CollectNodes(NRun.Instance!, creatures);
        var playerNode = AutoTestAssert.NotNull(creatures.FirstOrDefault(c => c.Entity == player.Creature), "Live Guardian NCreature");
        var sprite = AutoTestAssert.NotNull(playerNode.Visuals.Body as Sprite2D, "Live Guardian Sprite2D");
        AutoTestAssert.Equal(GuardianMechanics.IdleSpritePath, sprite.Texture?.ResourcePath, "Guardian starts in idle sprite");

        await GuardianMechanics.EnterDefensiveModeAsync(ctx, player, turns: 1, baseBlock: 16, triggerModeShiftListeners: false);
        AutoTestAssert.Equal(GuardianMechanics.DefensiveSpritePath, sprite.Texture?.ResourcePath, "DefensiveModePower swapped sprite to guardian_defensive.png");

        await PowerCmd.Remove<DefensiveModePower>(player.Creature);
        AutoTestAssert.Equal(GuardianMechanics.IdleSpritePath, sprite.Texture?.ResourcePath, "Exiting DefensiveModePower restored guardian.png");
        AutoTestAssert.True(pcs.OrbQueue.Orbs.Contains(stasis), "StasisOrb still mounted cleanly");
    }

    internal static async Task TestStarterGemsStasisAndRestSite()
    {
        var (player, runState, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGuardian>("GUARDIAN_CORE");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var ctx = new ThrowingPlayerChoiceContext();

        // 1. Verify ModeShifter Turn-1 effects: ModeShiftPower(20), GearUp in Hand, starter Common Gem in Deck!
        AutoTestAssert.Equal(20, player.Creature.GetPowerAmount<ModeShiftPower>(), "ModeShifter applied ModeShiftPower(20) on Turn 1");
        var gearUp = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<GearUp>().FirstOrDefault(), "ModeShifter added GearUp to Hand on Turn 1");
        var modeShifter = AutoTestAssert.NotNull(player.Relics.OfType<ModeShifter>().FirstOrDefault(), "Guardian starts with ModeShifter");
        AutoTestAssert.True(modeShifter.StarterGemGranted, "ModeShifter marked StarterGemGranted=true");
        var msRoundTrip = (ModeShifter)RelicModel.FromSerializable(modeShifter.ToSerializable());
        AutoTestAssert.True(msRoundTrip.StarterGemGranted, "ModeShifter [SavedProperty] round-trips StarterGemGranted=true");
        AutoTestAssert.True(player.Deck.Cards.Any(c => c is GuardianCard { IsGem: true }), "ModeShifter added a starter Common Gem to Deck");

        // 2. Native Rest Site SocketGemRestSiteOption test (including cancel path + socket path)!
        // Add a deterministic GemRed to Deck and enter a real RestSiteRoom!
        var deckRuby = runState.CreateCard<GemRed>(player);
        await CardPileCmd.Add(deckRuby, PileType.Deck);
        var deckTwinSlam = AutoTestAssert.NotNull(player.Deck.Cards.OfType<TwinSlam>().FirstOrDefault(), "Deck has TwinSlam");

        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, MegaCrit.Sts2.Core.Map.MapPointType.RestSite, null, showTransition: false);
        var restOptions = RestSiteOption.Generate(player);
        var socketOption = AutoTestAssert.NotNull(restOptions.OfType<SocketGemRestSiteOption>().FirstOrDefault(), "ModeShifter added SocketGemRestSiteOption");
        AutoTestAssert.True(socketOption.IsEnabled, "SocketGemRestSiteOption enabled with Gem + open-socket TwinSlam");
        AutoTestAssert.True(socketOption.Title.Exists(), "SocketGemRestSiteOption title loc exists");
        AutoTestAssert.True(socketOption.Description.Exists(), "SocketGemRestSiteOption description loc exists");

        // Cancel path: returning empty selection returns false without mutating Deck or TwinSlam!
        int deckCountBeforeCancel = player.Deck.Cards.Count;
        using (DownfallAutoTestRunner.CancelCardSelection())
        {
            bool cancelled = await socketOption.OnSelect();
            AutoTestAssert.True(!cancelled, "SocketGemRestSiteOption.OnSelect() returns false when cancelled");
        }
        AutoTestAssert.Equal(deckCountBeforeCancel, player.Deck.Cards.Count, "Cancelled socket left Deck unchanged");
        AutoTestAssert.Equal(0, deckTwinSlam.GetSocketedGems().Count, "Cancelled socket left TwinSlam unsocketed");

        // Socket deckRuby into deckTwinSlam via native OnSelect()!
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, deckRuby) || ReferenceEquals(c, deckTwinSlam)))
        {
            bool succeeded = await socketOption.OnSelect();
            AutoTestAssert.True(succeeded, "SocketGemRestSiteOption.OnSelect() succeeded");
        }
        AutoTestAssert.True(!player.Deck.Cards.Contains(deckRuby), "Socketed GemRed removed from Deck");
        AutoTestAssert.Equal(1, deckTwinSlam.GetSocketedGems().Count, "TwinSlam now has 1 socketed Gem");
        AutoTestAssert.Equal(GemType.Red, deckTwinSlam.GetSocketedGems()[0], "TwinSlam socketed with GemType.Red");
        AutoTestAssert.Equal(0, deckTwinSlam.OpenSockets, "Base TwinSlam now has 0 open sockets");

        // Verify socket persistence across MutableClone() and [SavedProperty] serialization!
        var twinClone = (TwinSlam)deckTwinSlam.MutableClone();
        AutoTestAssert.Equal("Red", twinClone.SocketedGemsCsv, "MutableClone preserves SocketedGemsCsv");
        var twinRoundTrip = (TwinSlam)CardModel.FromSerializable(deckTwinSlam.ToSerializable());
        AutoTestAssert.Equal("Red", twinRoundTrip.SocketedGemsCsv, "[SavedProperty] round-trip preserves SocketedGemsCsv");
        var twinDesc = deckTwinSlam.GetDescriptionForPile(PileType.Deck);
        AutoTestAssert.True(twinDesc.Contains("Ruby"), $"Socketed TwinSlam description mentions Ruby: {twinDesc}");

        // With 0 open-socket cards left in Deck, SocketGemRestSiteOption must be disabled!
        AutoTestAssert.True(!socketOption.IsEnabled, "SocketGemRestSiteOption disabled when 0 open-socket cards remain");

        // 3. Enter a fresh CombatRoom to verify socketed TwinSlam + SecondStrike copy, Mode Shift, Defensive Mode Block retention, Stasis round-trip, & all 12 Gems!
        var encounter = ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.NibbitsWeak>().ToMutable();
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MegaCrit.Sts2.Core.Map.MapPointType.Monster, encounter, showTransition: false);
        var combatRoom = AutoTestAssert.NotNull(runState.CurrentRoom as CombatRoom, "CombatRoom 2");
        combatState = AutoTestAssert.NotNull(combatRoom.CombatState, "CombatState 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PCS 2");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        await CreatureCmd.GainMaxHp(enemy, 1000m);

        // Verify socketed TwinSlam propagated from Deck into Combat!
        var combatTwin = AutoTestAssert.NotNull(pcs.AllCards.OfType<TwinSlam>().FirstOrDefault(), "Socketed TwinSlam in combat");
        AutoTestAssert.Equal("Red", combatTwin.SocketedGemsCsv, "Combat TwinSlam inherited socketed Ruby from Deck");

        await ClearHandAndRefillEnergy(player, pcs);
        await CardPileCmd.Add(combatTwin, PileType.Hand);
        int eHp = enemy.CurrentHp;
        AutoTestAssert.True(combatTwin.TryManualPlay(enemy), "Play socketed TwinSlam");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(eHp - 7, enemy.CurrentHp, "TwinSlam dealt 7 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Socketed Ruby on TwinSlam granted +2 temp Strength");
        var secondStrike = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<SecondStrike>().FirstOrDefault(), "TwinSlam added SecondStrike to Hand");
        AutoTestAssert.Equal("Red", secondStrike.SocketedGemsCsv, "SecondStrike copied Ruby socket from TwinSlam");

        AutoTestAssert.True(secondStrike.TryManualPlay(enemy), "Play socketed SecondStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<StrengthPower>(), "Socketed Ruby on SecondStrike stacked +2 temp Strength (2 -> 4)");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(secondStrike), "SecondStrike exhausted");
        await PowerCmd.Remove<FlexPotionPower>(player.Creature);
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 4. CurlUp (Stasis + Brace 10) + GearUp (Brace 10) -> triggers ModeShiftPower (16 Block, DefensiveModePower(1), +3 Thorns, threshold 20 -> 30)!
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var targetForStasis = combatState.CreateCard<GuardianStrike>(player);
        var curlUp = combatState.CreateCard<CurlUp>(player);
        var gearUpCard = combatState.CreateCard<GearUp>(player);
        await CardPileCmd.Add(targetForStasis, PileType.Hand);
        await CardPileCmd.Add(curlUp, PileType.Hand);
        await CardPileCmd.Add(gearUpCard, PileType.Hand);

        AutoTestAssert.True(curlUp.TryManualPlay(null), "Play CurlUp (Stasis random hand card + Brace 10)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<ModeShiftPower>(), "CurlUp Braced ModeShiftPower 20 -> 10");
        AutoTestAssert.Equal(1, GuardianMechanics.GetStasisOrbs(player).Count, "CurlUp placed 1 card into Stasis");

        // Trigger Mode Shift via Brace 10!
        await ClearHandAndRefillEnergy(player, pcs);
        var gear2 = combatState.CreateCard<GearUp>(player);
        await CardPileCmd.Add(gear2, PileType.Hand);
        AutoTestAssert.True(gear2.TryManualPlay(null), "Play GearUp (Brace 10 -> triggers Mode Shift!)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(16, player.Creature.Block, "Mode Shift granted 16 Block");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DefensiveModePower>(), "Mode Shift entered DefensiveModePower(1)");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ThornsPower>(), "DefensiveModePower granted 3 Thorns");
        AutoTestAssert.Equal(30, player.Creature.GetPowerAmount<ModeShiftPower>(), "ModeShiftPower threshold escalated 20 -> 30");

        // End Round 1 -> Round 2: DefensiveModePower RETAINS Block across turn boundary, then decrements 1 -> 0 and removes 3 Thorns!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.True(player.Creature.Block > 0, $"DefensiveModePower retained Block into Round 2 (Block={player.Creature.Block})");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DefensiveModePower>(), "DefensiveModePower expired at start of Round 2 after retaining Block");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ThornsPower>(), "DefensiveModePower removed 3 Thorns on exit");

        // 5. Stasis full round-trip (Hand -> Stasis -> Accelerate to 0 -> Hand costing 0 -> Play for 0 -> Discard)
        //    + Non-Stasis orb coexistence (evicts Defect LightningOrb before refusing when all 3 slots are Stasis)!
        await ClearHandAndRefillEnergy(player, pcs);
        foreach (var existing in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, existing);

        await ClearHandAndRefillEnergy(player, pcs);
        var rtStrike = combatState.CreateCard<GuardianStrike>(player);
        await CardPileCmd.Add(rtStrike, PileType.Hand);
        var rtOrb = AutoTestAssert.NotNull(await GuardianMechanics.PlaceInStasisAsync(ctx, player, rtStrike), "Place rtStrike into Stasis");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(rtStrike), "rtStrike removed from Hand while in Stasis");
        AutoTestAssert.Equal(2, rtOrb.TurnsRemaining, "1-cost rtStrike has 2 turns in Stasis");

        await GuardianMechanics.AccelerateAsync(ctx, player, 2);
        AutoTestAssert.Equal(0, GuardianMechanics.GetStasisOrbs(player).Count, "Accelerating 2x evoked rtOrb");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(rtStrike), "rtStrike returned to Hand after Stasis");
        AutoTestAssert.Equal(0, rtStrike.EnergyCost.GetWithModifiers(CostModifiers.All), "Released Stasis card costs 0 until played");
        int energyBeforeFree = pcs.Energy;
        AutoTestAssert.True(rtStrike.TryManualPlay(enemy), "Play released 0-cost rtStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeFree, pcs.Energy, "Released Stasis card spent 0 Energy");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(rtStrike), "Played Stasis card landed cleanly in Discard Pile");

        // Non-Stasis orb eviction + 3-Stasis full-slot refusal!
        await OrbCmd.Channel<LightningOrb>(ctx, player);
        var s1 = combatState.CreateCard<GuardianDefend>(player);
        var s2 = combatState.CreateCard<GuardianDefend>(player);
        var s3 = combatState.CreateCard<GuardianDefend>(player);
        var s4 = combatState.CreateCard<GuardianDefend>(player);
        await CardPileCmd.Add(s1, PileType.Hand);
        await CardPileCmd.Add(s2, PileType.Hand);
        await CardPileCmd.Add(s3, PileType.Hand);
        await CardPileCmd.Add(s4, PileType.Hand);
        AutoTestAssert.NotNull(await GuardianMechanics.PlaceInStasisAsync(ctx, player, s1), "Stasis slot 1");
        AutoTestAssert.NotNull(await GuardianMechanics.PlaceInStasisAsync(ctx, player, s2), "Stasis slot 2");
        AutoTestAssert.NotNull(await GuardianMechanics.PlaceInStasisAsync(ctx, player, s3), "Stasis slot 3 evicted LightningOrb");
        AutoTestAssert.Equal(3, GuardianMechanics.GetStasisOrbs(player).Count, "3 StasisOrbs occupy all 3 slots");
        AutoTestAssert.True(await GuardianMechanics.PlaceInStasisAsync(ctx, player, s4) is null, "4th Stasis placement refused when all 3 slots hold StasisOrbs");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(s4), "Refused 4th card remained in Hand");

        // 6. Verify ALL 12 Gem cards in combat (none Exhaust!) + ModeShifterPlus Orobas upgrade!
        foreach (var o in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, o);

        await ClearHandAndRefillEnergy(player, pcs);
        await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Draw, CardPilePosition.Top);

        // Place 1 card in Stasis for GemYellow (Amber: Accelerate)!
        var amberTarget = combatState.CreateCard<GuardianStrike>(player);
        await CardPileCmd.Add(amberTarget, PileType.Hand);
        var amberOrb = AutoTestAssert.NotNull(await GuardianMechanics.PlaceInStasisAsync(ctx, player, amberTarget), "Place amberTarget in Stasis");

        // Apply WeakPower(2) for GemSynthetic (Onyx: reduce debuffs by 1)!
        await PowerCmd.Apply<WeakPower>(ctx, player.Creature, 2m, enemy, null);

        var gemsToTest = new GuardianGemCard[]
        {
            (GuardianGemCard)combatState.CreateCard<GemRed>(player),
            (GuardianGemCard)combatState.CreateCard<GemGreen>(player),
            (GuardianGemCard)combatState.CreateCard<GemLightblue>(player),
            (GuardianGemCard)combatState.CreateCard<GemBlue>(player),
            (GuardianGemCard)combatState.CreateCard<GemFragmented>(player),
            (GuardianGemCard)combatState.CreateCard<GemCyan>(player),
            (GuardianGemCard)combatState.CreateCard<GemCrimson>(player),
            (GuardianGemCard)combatState.CreateCard<GemWhite>(player),
            (GuardianGemCard)combatState.CreateCard<GemYellow>(player),
            (GuardianGemCard)combatState.CreateCard<GemPurple>(player),
            (GuardianGemCard)combatState.CreateCard<GemOrange>(player),
            (GuardianGemCard)combatState.CreateCard<GemSynthetic>(player),
        };

        int modeShiftBeforeBlue = player.Creature.GetPowerAmount<ModeShiftPower>();
        int energyBeforeOrange = 0;
        int handBeforeWhite = 0;
        int handAfterWhite = 0;
        foreach (var gem in gemsToTest)
        {
            AutoTestAssert.True(!gem.IsUpgradable, $"{gem.Id} is non-upgradable");
            await CardPileCmd.Add(gem, PileType.Hand);
            if (gem is GemOrange)
                energyBeforeOrange = pcs.Energy;
            if (gem is GemWhite)
                handBeforeWhite = pcs.Hand.Cards.Count;
            AutoTestAssert.True(gem.TryManualPlay(null), $"Play {gem.Id}");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            if (gem is GemWhite)
                handAfterWhite = pcs.Hand.Cards.Count;
            AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(gem), $"{gem.Id} went to Discard Pile (did NOT exhaust)");
        }

        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "GemRed granted +2 temp Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "GemGreen granted +2 temp Dexterity");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<SlimeTempThornsPower>(), "GemLightblue granted +4 temp Thorns");
        AutoTestAssert.Equal(modeShiftBeforeBlue - 4, player.Creature.GetPowerAmount<ModeShiftPower>(), "GemBlue Braced 4");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<CrystalShiv>().Any(), "GemFragmented added CrystalShiv");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<CrystalWard>().Any(), "GemCyan added CrystalWard");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "GemCrimson applied 1 Vulnerable");
        AutoTestAssert.Equal(handBeforeWhite, handAfterWhite, "GemWhite drew 1 card (replacing itself in Hand)");
        AutoTestAssert.Equal(1, amberOrb.TurnsRemaining, "GemYellow accelerated amberOrb 2 -> 1");
        AutoTestAssert.Equal(-2, enemy.GetPowerAmount<StrengthPower>(), "GemPurple reduced enemy Strength by 2 this turn");
        AutoTestAssert.Equal(energyBeforeOrange + 1, pcs.Energy, "GemOrange granted +1 Energy");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WeakPower>(), "GemSynthetic reduced WeakPower 2 -> 1");

        // 7. Hand-full (10-card) Stasis delay + release when Hand drops below 10 + Double-Evoke idempotency!
        await ClearHandAndRefillEnergy(player, pcs);
        while (pcs.Hand.Cards.Count < 10)
            await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Hand);
        await GuardianMechanics.AccelerateAsync(ctx, player, 1);
        AutoTestAssert.Equal(1, GuardianMechanics.GetStasisOrbs(player).Count, "Full 10-card Hand delayed non-Volatile Stasis release");
        AutoTestAssert.Equal(0, amberOrb.TurnsRemaining, "Delayed StasisOrb stayed at 0 turns");
        await CardPileCmd.Add(pcs.Hand.Cards.First(), PileType.Discard);
        await GuardianMechanics.AccelerateAsync(ctx, player, 1);
        AutoTestAssert.Equal(0, GuardianMechanics.GetStasisOrbs(player).Count, "StasisOrb released once Hand dropped below 10");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(amberTarget), "amberTarget returned to Hand");

        // 8. ModeShiftPower HP-loss trigger + multi-trigger excess Brace (stays on player at positive threshold!) + Orobas ModeShifterPlus!
        await RelicCmd.Replace(modeShifter, ModelDb.Relic<ModeShifterPlus>().ToMutable());
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Draw, CardPilePosition.Top);

        int msBeforeDmg = player.Creature.GetPowerAmount<ModeShiftPower>();
        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Unblockable | ValueProp.Unpowered, enemy);
        AutoTestAssert.Equal(msBeforeDmg - 5, player.Creature.GetPowerAmount<ModeShiftPower>(), "Unblocked HP loss reduced ModeShiftPower by 5");

        // Multi-trigger excess Brace: reset ModeShiftPower to 20 (activations=1 -> next thresholds 30, 40) -> Brace 55 triggers 2x and stays on player!
        await PowerCmd.Remove<ModeShiftPower>(player.Creature);
        await PowerCmd.Remove<DefensiveModePower>(player.Creature);
        await PowerCmd.Apply<ModeShiftPower>(ctx, player.Creature, 20m, player.Creature, null);
        int energyBeforeShift = pcs.Energy;
        await GuardianMechanics.BraceAsync(ctx, player, 55);
        AutoTestAssert.Equal(35, player.Creature.GetPowerAmount<ModeShiftPower>(), "Multi-trigger excess Brace(55) from 20 triggered 2x (+30, +40) leaving ModeShiftPower(35) alive");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DefensiveModePower>(), "2x Mode Shift stacked DefensiveModePower(2)");
        AutoTestAssert.Equal(energyBeforeShift + 2, pcs.Energy, "ModeShifterPlus granted +1 Energy per Mode Shift trigger (+2)");
        AutoTestAssert.True(pcs.Hand.Cards.Count >= 2, "ModeShifterPlus drew cards on Mode Shift");
    }

    internal static async Task TestCommonCardsInCombat()
    {
        var (player, runState, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGuardian>("GUARDIAN_COM");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

        // 1. ChargeCore (10 dmg + SelfStasis 2 turns + Tick Draw 1 + Volatile Exhaust!) + ShieldCharger (SelfStasis 3 turns + Tick 10 Block + Brace 4 + Volatile!)
        //    + Incinerate+ (8 dmg + Accelerate 2 -> triggers ChargeCore Tick twice AND exhausts Volatile ChargeCore via CardCmd.Exhaust!)
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<GuardianDefend>(player), PileType.Draw, CardPilePosition.Top);

        var chargeCore = combatState.CreateCard<ChargeCore>(player);
        var shieldCharger = combatState.CreateCard<ShieldCharger>(player);
        var incineratePlus = combatState.CreateCard<Incinerate>(player);
        CardCmd.Upgrade(incineratePlus);
        await CardPileCmd.Add(chargeCore, PileType.Hand);
        await CardPileCmd.Add(shieldCharger, PileType.Hand);
        await CardPileCmd.Add(incineratePlus, PileType.Hand);

        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(chargeCore.TryManualPlay(enemy1), "Play ChargeCore");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 10, enemy1.CurrentHp, "ChargeCore dealt 10 damage");
        var ccOrb = AutoTestAssert.NotNull(GuardianMechanics.GetStasisOrbs(player).FirstOrDefault(o => ReferenceEquals(o.StasisCard, chargeCore)), "ChargeCore entered Stasis");
        AutoTestAssert.Equal(2, ccOrb.TurnsRemaining, "1-cost ChargeCore has 2 turns in Stasis");

        AutoTestAssert.True(shieldCharger.TryManualPlay(null), "Play ShieldCharger");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var scOrb = AutoTestAssert.NotNull(GuardianMechanics.GetStasisOrbs(player).FirstOrDefault(o => ReferenceEquals(o.StasisCard, shieldCharger)), "ShieldCharger entered Stasis");
        AutoTestAssert.Equal(3, scOrb.TurnsRemaining, "2-cost ShieldCharger has 3 turns in Stasis");

        // Incinerate+ accelerates oldest orb (ChargeCore) twice -> draws 2 cards AND exhausts Volatile ChargeCore!
        int handBeforeInc = pcs.Hand.Cards.Count;
        AutoTestAssert.True(incineratePlus.TryManualPlay(enemy1), "Play Incinerate+ (Accelerate 2 -> ticks & exhausts ChargeCore)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBeforeInc - 1 + 2, pcs.Hand.Cards.Count, "ChargeCore Tick drew 2 cards across 2 Accelerates");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(chargeCore), "Volatile ChargeCore exhausted cleanly via CardCmd.Exhaust");
        AutoTestAssert.Equal(1, GuardianMechanics.GetStasisOrbs(player).Count, "Only ShieldCharger remains in Stasis");

        // TemporalShield (8 Block + Accelerate -> ticks ShieldCharger for +10 Block & Brace 4!) + TemporalStrike (7 dmg + 1 Energy while Stasis active!)
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int msBeforeTs = player.Creature.GetPowerAmount<ModeShiftPower>();
        var tShield = combatState.CreateCard<TemporalShield>(player);
        var tStrike = combatState.CreateCard<TemporalStrike>(player);
        await CardPileCmd.Add(tShield, PileType.Hand);
        await CardPileCmd.Add(tStrike, PileType.Hand);

        AutoTestAssert.True(tShield.TryManualPlay(null), "Play TemporalShield (8 Block + Accelerate ShieldCharger: +10 Block & Brace 4)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(18, player.Creature.Block, "TemporalShield (8) + ShieldCharger Tick (10) = 18 Block");
        AutoTestAssert.Equal(msBeforeTs - 4, player.Creature.GetPowerAmount<ModeShiftPower>(), "ShieldCharger Tick Braced 4");
        AutoTestAssert.Equal(2, scOrb.TurnsRemaining, "ShieldCharger ticked 3 -> 2");

        int energyBeforeTStrike = pcs.Energy;
        AutoTestAssert.True(tStrike.TryManualPlay(enemy1), "Play TemporalStrike with Stasis active (+1 Energy net 0 cost)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeTStrike, pcs.Energy, "TemporalStrike refunded 1 Energy with Stasis active");

        // 2. Reroute (9 dmg + ClonePower(1), does NOT place itself into Stasis!) -> next Defend placed into Stasis!
        await ClearHandAndRefillEnergy(player, pcs);
        var reroute = combatState.CreateCard<Reroute>(player);
        var reroutedDef = combatState.CreateCard<GuardianDefend>(player);
        await CardPileCmd.Add(reroute, PileType.Hand);
        await CardPileCmd.Add(reroutedDef, PileType.Hand);

        AutoTestAssert.True(reroute.TryManualPlay(enemy1), "Play Reroute");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ClonePower>(), "Reroute applied ClonePower(1) without consuming on itself");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(reroute), "Reroute landed in Discard Pile");

        AutoTestAssert.True(reroutedDef.TryManualPlay(null), "Play Defend -> placed into Stasis by ClonePower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ClonePower>(), "ClonePower decremented to 0");
        AutoTestAssert.True(GuardianMechanics.GetStasisOrbs(player).Any(o => ReferenceEquals(o.StasisCard, reroutedDef)), "Defend placed into Stasis by ClonePower");

        // 3. Suspension (6 Block + choose hand card to Stasis), CrystalBeam (12 + 2 * gems in deck), PolyBeam (2x4=8), WalkerClaw (5x3=15), PrimingBeam (8 dmg + Brace 4)!
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var suspTarget = combatState.CreateCard<GuardianStrike>(player);
        var suspension = combatState.CreateCard<Suspension>(player);
        await CardPileCmd.Add(suspTarget, PileType.Hand);
        await CardPileCmd.Add(suspension, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, suspTarget)))
        {
            AutoTestAssert.True(suspension.TryManualPlay(null), "Play Suspension placing chosen Strike into Stasis");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(6, player.Creature.Block, "Suspension granted 6 Block");
        AutoTestAssert.True(GuardianMechanics.GetStasisOrbs(player).Any(o => ReferenceEquals(o.StasisCard, suspTarget)), "Suspension placed chosen card into Stasis");

        // Add 1 loose Gem + 1 socketed Gem in Deck -> CrystalBeam deals 12 + 2*(starterGem + 2) damage!
        var extraDeckGem = runState.CreateCard<GemRed>(player);
        await CardPileCmd.Add(extraDeckGem, PileType.Deck);
        var deckTwin = AutoTestAssert.NotNull(player.Deck.Cards.OfType<TwinSlam>().FirstOrDefault(), "Deck TwinSlam");
        deckTwin.TrySocketGem(GemType.Blue);
        int expectedDeckGems = player.Deck.Cards.OfType<GuardianCard>().Sum(c => (c.IsGem ? 1 : 0) + c.GetSocketedGems().Count);
        AutoTestAssert.True(expectedDeckGems >= 3, $"Deck has >= 3 total Gems (got {expectedDeckGems})");

        await ClearHandAndRefillEnergy(player, pcs);
        var crystalBeam = combatState.CreateCard<CrystalBeam>(player);
        var polyBeam = combatState.CreateCard<PolyBeam>(player);
        var walkerClaw = combatState.CreateCard<WalkerClaw>(player);
        var primingBeam = combatState.CreateCard<PrimingBeam>(player);
        await CardPileCmd.Add(crystalBeam, PileType.Hand);
        await CardPileCmd.Add(polyBeam, PileType.Hand);
        await CardPileCmd.Add(walkerClaw, PileType.Hand);
        await CardPileCmd.Add(primingBeam, PileType.Hand);

        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(crystalBeam.TryManualPlay(enemy1), "Play CrystalBeam");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - (12 + 2 * expectedDeckGems), enemy1.CurrentHp, "CrystalBeam scaled with loose + socketed deck Gems");

        int totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(polyBeam.TryManualPlay(null), "Play PolyBeam (2x4 = 8 total dmg)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 8, enemy1.CurrentHp + enemy2.CurrentHp, "PolyBeam dealt 8 total damage");

        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(walkerClaw.TryManualPlay(enemy1), "Play WalkerClaw (5x3 = 15 dmg)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 15, enemy1.CurrentHp, "WalkerClaw dealt 15 damage");

        int msBeforePrime = player.Creature.GetPowerAmount<ModeShiftPower>();
        AutoTestAssert.True(primingBeam.TryManualPlay(enemy1), "Play PrimingBeam (8 dmg + Brace 4)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(msBeforePrime - 4, player.Creature.GetPowerAmount<ModeShiftPower>(), "PrimingBeam Braced 4");

        // 4. RollAttack (normal: single target + Brace 8; Defensive Mode: AoE!), StrikeForStrike (13 dmg + enemy hits back for 3 triggering Thorns!),
        //    PiercingHide (7 Block + 2 Temp Thorns + Brace 3), Planning (12 Block + reduce debuffs 1), DecasProtection (+3 temp Str/Dex), ChargeUp (7 Block + NextTurnTempStrength 2)!
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<DefensiveModePower>(player.Creature);
        await PowerCmd.Remove<ThornsPower>(player.Creature);
        var roll1 = combatState.CreateCard<RollAttack>(player);
        await CardPileCmd.Add(roll1, PileType.Hand);
        AutoTestAssert.Equal(TargetType.AnyEnemy, roll1.TargetType, "RollAttack targets AnyEnemy outside Defensive Mode");
        int msBeforeRoll = player.Creature.GetPowerAmount<ModeShiftPower>();
        e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(roll1.TryManualPlay(enemy1), "Play RollAttack outside Defensive Mode (16 single-target + Brace 8)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 16, enemy1.CurrentHp, "RollAttack dealt 16 to enemy1");
        AutoTestAssert.Equal(e2Hp, enemy2.CurrentHp, "RollAttack did NOT hit enemy2 outside Defensive Mode");
        AutoTestAssert.True(player.Creature.GetPowerAmount<ModeShiftPower>() != msBeforeRoll, "RollAttack Braced 8 outside Defensive Mode");

        // Now enter Defensive Mode -> already-in-hand RollAttack flips live to TargetType.AllEnemies!
        var roll2 = combatState.CreateCard<RollAttack>(player);
        await CardPileCmd.Add(roll2, PileType.Hand);
        AutoTestAssert.Equal(TargetType.AnyEnemy, roll2.TargetType, "In-hand RollAttack starts AnyEnemy outside Defensive Mode");
        await GuardianMechanics.EnterDefensiveModeAsync(ctx, player, turns: 1, baseBlock: 0, triggerModeShiftListeners: false);
        AutoTestAssert.Equal(TargetType.AllEnemies, roll2.TargetType, "Already-in-hand RollAttack flips live to AllEnemies in Defensive Mode");
        e1Hp = enemy1.CurrentHp;
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(roll2.TryManualPlay(null), "Play RollAttack in Defensive Mode (16 AoE)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 16, enemy1.CurrentHp, "RollAttack hit enemy1 for 16 in Defensive Mode");
        AutoTestAssert.Equal(e2Hp - 16, enemy2.CurrentHp, "RollAttack hit enemy2 for 16 in Defensive Mode");

        // StrikeForStrike: deals 13 dmg + enemy hits player for 3 (triggering Defensive Mode's 3 Thorns back onto enemy1!)
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int hpBeforeSfs = player.Creature.CurrentHp;
        e1Hp = enemy1.CurrentHp;
        var sfs = combatState.CreateCard<StrikeForStrike>(player);
        await CardPileCmd.Add(sfs, PileType.Hand);
        AutoTestAssert.True(sfs.TryManualPlay(enemy1), "Play StrikeForStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeSfs - 3, player.Creature.CurrentHp, "StrikeForStrike enemy counter-attack dealt 3 damage");
        AutoTestAssert.Equal(e1Hp - 13 - 3, enemy1.CurrentHp, "StrikeForStrike dealt 13 + triggered 3 Thorns from enemy counter-attack");

        // Planning (12 Block + reduce debuffs 1), PiercingHide (7 Block + 2 Temp Thorns + Brace 3), DecasProtection (+3 temp Str/Dex + 1 socket), ChargeUp (7 Block + NextTurnTempStrength 2 + 1 socket)!
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Apply<WeakPower>(ctx, player.Creature, 2m, enemy1, null);
        var planning = combatState.CreateCard<Planning>(player);
        var pHide = combatState.CreateCard<PiercingHide>(player);
        var deca = combatState.CreateCard<DecasProtection>(player);
        var chargeUp = combatState.CreateCard<ChargeUp>(player);
        AutoTestAssert.Equal(1, deca.MaxSockets, "DecasProtection has 1 socket");
        AutoTestAssert.Equal(1, chargeUp.MaxSockets, "ChargeUp has 1 socket");
        AutoTestAssert.True(deca.TrySocketGem(GemType.Orange), "Socket Citrine (+1 Energy) into DecasProtection");
        AutoTestAssert.True(chargeUp.TrySocketGem(GemType.Orange), "Socket Citrine (+1 Energy) into ChargeUp");
        await CardPileCmd.Add(planning, PileType.Hand);
        await CardPileCmd.Add(pHide, PileType.Hand);
        await CardPileCmd.Add(deca, PileType.Hand);
        await CardPileCmd.Add(chargeUp, PileType.Hand);

        AutoTestAssert.True(planning.TryManualPlay(null), "Play Planning");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WeakPower>(), "Planning reduced WeakPower 2 -> 1");

        AutoTestAssert.True(pHide.TryManualPlay(null), "Play PiercingHide");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<SlimeTempThornsPower>(), "PiercingHide applied 2 Temp Thorns");

        int energyBeforeDeca = pcs.Energy;
        AutoTestAssert.True(deca.TryManualPlay(null), "Play DecasProtection");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "DecasProtection granted +3 temp Strength");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<DexterityPower>(), "DecasProtection granted +3 temp Dexterity");
        AutoTestAssert.Equal(energyBeforeDeca, pcs.Energy, "DecasProtection triggered socketed Citrine (-1 cost +1 energy)");

        int energyBeforeChargeUp = pcs.Energy;
        AutoTestAssert.True(chargeUp.TryManualPlay(null), "Play ChargeUp");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<NextTurnTempStrengthPower>(), "ChargeUp applied NextTurnTempStrengthPower(2)");
        AutoTestAssert.Equal(energyBeforeChargeUp, pcs.Energy, "ChargeUp triggered socketed Citrine (-1 cost +1 energy)");

        // 5. ClonePower edge cases: stacking Reroute, skipping Exhaust & SelfStasis, and SelfStasis fallback to Discard when all 3 Stasis slots are full!
        await ClearHandAndRefillEnergy(player, pcs);
        foreach (var orb in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, orb);
        await ClearHandAndRefillEnergy(player, pcs);

        var rerouteA = combatState.CreateCard<Reroute>(player);
        var rerouteB = combatState.CreateCard<Reroute>(player);
        var exhaustCard = combatState.CreateCard<SecondStrike>(player); // Exhaust token
        var selfStasisCard = combatState.CreateCard<ChargeCore>(player);
        var filler1 = combatState.CreateCard<GuardianDefend>(player);
        var overflowSelfStasis = combatState.CreateCard<ChargeCore>(player);
        await CardPileCmd.Add(rerouteA, PileType.Hand);
        await CardPileCmd.Add(rerouteB, PileType.Hand);
        await CardPileCmd.Add(exhaustCard, PileType.Hand);
        await CardPileCmd.Add(selfStasisCard, PileType.Hand);
        await CardPileCmd.Add(filler1, PileType.Hand);
        await CardPileCmd.Add(overflowSelfStasis, PileType.Hand);

        AutoTestAssert.True(rerouteA.TryManualPlay(enemy1), "Play Reroute #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ClonePower>(), "Reroute #1 granted ClonePower(1)");
        AutoTestAssert.Equal(0, GuardianMechanics.GetStasisOrbs(player).Count, "Reroute #1 did not self-stasis");

        // Playing Exhaust card while ClonePower is active does NOT consume ClonePower
        AutoTestAssert.True(exhaustCard.TryManualPlay(enemy1), "Play Exhaust card with ClonePower active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ClonePower>(), "Exhaust card did not consume ClonePower");

        // Playing SelfStasis card while ClonePower is active enters Stasis via IsSelfStasis and does NOT consume ClonePower
        AutoTestAssert.True(selfStasisCard.TryManualPlay(enemy1), "Play SelfStasis ChargeCore with ClonePower active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ClonePower>(), "SelfStasis card did not consume ClonePower");
        AutoTestAssert.True(GuardianMechanics.GetStasisOrbs(player).Any(o => ReferenceEquals(o.StasisCard, selfStasisCard)), "ChargeCore entered Stasis slot 1");

        // Stacking Reroute #2 while ClonePower(1) is active: #2 adds +1 (to 2), then consumes 1 -> enters Stasis slot 2, leaving ClonePower(1)
        AutoTestAssert.True(rerouteB.TryManualPlay(enemy1), "Play Reroute #2 while ClonePower(1) active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ClonePower>(), "Stacked Reroute #2 left ClonePower(1)");
        AutoTestAssert.True(GuardianMechanics.GetStasisOrbs(player).Any(o => ReferenceEquals(o.StasisCard, rerouteB)), "Stacked Reroute #2 entered Stasis slot 2");

        // Next regular card fills Stasis slot 3 via remaining ClonePower(1)
        AutoTestAssert.True(filler1.TryManualPlay(null), "Play Defend to fill Stasis slot 3 via ClonePower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ClonePower>(), "ClonePower consumed to 0");
        AutoTestAssert.Equal(3, GuardianMechanics.GetStasisOrbs(player).Count, "All 3 Stasis slots are now full");

        // Playing another SelfStasis card when all 3 Stasis slots are full lands cleanly in Discard!
        AutoTestAssert.True(overflowSelfStasis.TryManualPlay(enemy1), "Play SelfStasis ChargeCore with full Stasis");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, GuardianMechanics.GetStasisOrbs(player).Count, "Full Stasis stayed at 3 orbs");
        AutoTestAssert.Equal(PileType.Discard, overflowSelfStasis.Pile?.Type, "Overflow SelfStasis ChargeCore landed cleanly in Discard");

        // End Round 1 -> Round 2: ChargeUp grants +2 temporary Strength (FlexPotionPower) while DecasProtection temp Strength/Dex expired!
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<NextTurnTempStrengthPower>(), "NextTurnTempStrengthPower triggered and removed on Round 2");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "ChargeUp granted +2 temporary Strength on Round 2");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<FlexPotionPower>(), "ChargeUp applied FlexPotionPower(2) on Round 2");
    }

    internal static async Task ClearHandAndRefillEnergy(Player player, PlayerCombatState pcs)
    {
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        if (pcs.Energy < 10)
            await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
    }
}
