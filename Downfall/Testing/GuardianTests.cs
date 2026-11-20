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

    internal static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGuardian>("GUARDIAN_UNC1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

        // 1. BronzeArmor (Exhaust, ReduceDebuffs 2, Brace 8) & upgraded BronzeArmor+ (no Exhaust)
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Apply<WeakPower>(ctx, player.Creature, 3m, enemy1, null);
        int msBefore = player.Creature.GetPowerAmount<ModeShiftPower>();
        var ba = combatState.CreateCard<BronzeArmor>(player);
        var baUp = combatState.CreateCard<BronzeArmor>(player);
        CardCmd.Upgrade(baUp);
        await CardPileCmd.Add(ba, PileType.Hand);
        await CardPileCmd.Add(baUp, PileType.Hand);

        AutoTestAssert.True(ba.TryManualPlay(null), "Play BronzeArmor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WeakPower>(), "BronzeArmor reduced WeakPower 3 -> 1");
        AutoTestAssert.Equal(PileType.Exhaust, ba.Pile?.Type, "Unupgraded BronzeArmor Exhausted");
        AutoTestAssert.True(player.Creature.GetPowerAmount<ModeShiftPower>() != msBefore, "BronzeArmor Braced 8");

        AutoTestAssert.True(baUp.TryManualPlay(null), "Play upgraded BronzeArmor+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WeakPower>(), "BronzeArmor+ cleared remaining WeakPower");
        AutoTestAssert.Equal(PileType.Discard, baUp.Pile?.Type, "Upgraded BronzeArmor+ did not Exhaust");

        // 2. BronzeOrb (9 dmg, Brace for unblocked damage, Exhaust)
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<DefensiveModePower>(player.Creature);
        await PowerCmd.Remove<ThornsPower>(player.Creature);
        var msForOrb = player.Creature.GetPower<ModeShiftPower>()!;
        if (msForOrb.Amount < 15)
            await PowerCmd.ModifyAmount(ctx, msForOrb, 15 - msForOrb.Amount, player.Creature, null);
        await CreatureCmd.GainBlock(enemy1, 4m, ValueProp.Unpowered, null);
        msBefore = player.Creature.GetPowerAmount<ModeShiftPower>();
        var bronzeOrb = combatState.CreateCard<BronzeOrb>(player);
        await CardPileCmd.Add(bronzeOrb, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(bronzeOrb.TryManualPlay(enemy1), "Play BronzeOrb against 4 Block enemy (5 unblocked)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 5, enemy1.CurrentHp, "BronzeOrb dealt 5 unblocked damage");
        AutoTestAssert.Equal(msBefore - 5, player.Creature.GetPowerAmount<ModeShiftPower>(), "BronzeOrb Braced for exact unblocked damage (5)");
        AutoTestAssert.Equal(PileType.Exhaust, bronzeOrb.Pile?.Type, "BronzeOrb Exhausted");

        // 3. DonusPower (Draw 2 + ReduceDebuffs 1), GuardianHarden (4 PlatingPower), FloatingOrbs (3 dmg on 0-cost card, NO self-trigger on 0-cost FloatingOrbs, YES on Stasis-0 card, NO on X=0 MultiBeam)
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Apply<FrailPower>(ctx, player.Creature, 2m, enemy1, null);
        var donus = combatState.CreateCard<DonusPower>(player);
        var harden = combatState.CreateCard<GuardianHarden>(player);
        var floatOrbs = combatState.CreateCard<FloatingOrbs>(player);
        floatOrbs.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        var zeroCostShiv = combatState.CreateCard<CrystalShiv>(player);
        await CardPileCmd.Add(donus, PileType.Hand);
        await CardPileCmd.Add(harden, PileType.Hand);
        await CardPileCmd.Add(floatOrbs, PileType.Hand);
        await CardPileCmd.Add(zeroCostShiv, PileType.Hand);

        AutoTestAssert.True(donus.TryManualPlay(null), "Play DonusPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FrailPower>(), "DonusPower reduced FrailPower 2 -> 1");

        AutoTestAssert.True(harden.TryManualPlay(null), "Play GuardianHarden");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<PlatingPower>(), "GuardianHarden granted 4 PlatingPower");

        int totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(floatOrbs.TryManualPlay(null), "Play 0-cost FloatingOrbs (must NOT self-trigger)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<FloatingOrbsPower>(), "FloatingOrbs applied FloatingOrbsPower(3)");
        AutoTestAssert.Equal(totalHp, enemy1.CurrentHp + enemy2.CurrentHp, "0-cost FloatingOrbs did NOT self-trigger");

        AutoTestAssert.True(zeroCostShiv.TryManualPlay(enemy1), "Play 0-cost CrystalShiv (3 dmg + 3 FloatingOrbs)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 6, enemy1.CurrentHp + enemy2.CurrentHp, "FloatingOrbs dealt 3 extra damage on 0-cost play");

        // Stasis-released 0-cost card DOES trigger FloatingOrbs!
        var stasisDefend = combatState.CreateCard<GuardianDefend>(player);
        var defOrb = await GuardianMechanics.PlaceInStasisAsync(ctx, player, stasisDefend);
        AutoTestAssert.NotNull(defOrb, "Defend placed in Stasis for FloatingOrbs 0-cost test");
        await GuardianMechanics.AccelerateAllAsync(ctx, player, 2);
        AutoTestAssert.Equal(PileType.Hand, stasisDefend.Pile?.Type, "Stasis-released Defend returned to Hand at 0 cost");
        totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(stasisDefend.TryManualPlay(null), "Play Stasis-released 0-cost Defend");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 3, enemy1.CurrentHp + enemy2.CurrentHp, "FloatingOrbs triggered on Stasis-released 0-cost card");

        // X-cost MultiBeam played at X=0 Energy must NOT trigger FloatingOrbs!
        var zeroXBeam = combatState.CreateCard<MultiBeam>(player);
        await CardPileCmd.Add(zeroXBeam, PileType.Hand);
        if (pcs.Energy > 0)
            await PlayerCmd.LoseEnergy(pcs.Energy, player);
        totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(zeroXBeam.TryManualPlay(null), "Play X-cost MultiBeam at X=0 with FloatingOrbs active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp, enemy1.CurrentHp + enemy2.CurrentHp, "FloatingOrbs did NOT trigger on X=0 MultiBeam");

        // 4. EvasiveProtocol: (a) triggers ModeShift via Brace -> reduces debuffs; (b) already Defensive without ModeShift -> reduces once; (c) already Defensive AND triggers ModeShift -> reduces once via listener (no double-reduce)!
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<DefensiveModePower>(player.Creature);
        await PowerCmd.Remove<ThornsPower>(player.Creature);
        var msPower = player.Creature.GetPower<ModeShiftPower>()!;
        if (msPower.Amount > 5)
            await PowerCmd.ModifyAmount(ctx, msPower, 5 - msPower.Amount, player.Creature, null);
        await PowerCmd.Apply<WeakPower>(ctx, player.Creature, 6m, enemy1, null);
        var evasive1 = combatState.CreateCard<EvasiveProtocol>(player);
        await CardPileCmd.Add(evasive1, PileType.Hand);
        AutoTestAssert.True(evasive1.TryManualPlay(null), "Play EvasiveProtocol (triggers ModeShift via Brace 6 >= 5)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(player.Creature.HasPower<DefensiveModePower>(), "EvasiveProtocol entered Defensive Mode");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EvasiveProtocolPower>(), "EvasiveProtocolPower(1) applied");
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<WeakPower>(), "EvasiveProtocol triggered debuff reduction (6 -> 5) on ModeShift");

        // Case 3: Play second EvasiveProtocol while ALREADY in Defensive Mode (high ModeShift so Brace doesn't re-trigger)
        var evasive2 = combatState.CreateCard<EvasiveProtocol>(player);
        await CardPileCmd.Add(evasive2, PileType.Hand);
        AutoTestAssert.True(evasive2.TryManualPlay(null), "Play EvasiveProtocol while already in Defensive Mode");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<EvasiveProtocolPower>(), "EvasiveProtocolPower stacked to 2");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<WeakPower>(), "EvasiveProtocol reduced WeakPower (5 -> 4) immediately while in Defensive Mode");

        // Case 4: Play third EvasiveProtocol while ALREADY in Defensive Mode AND ModeShift <= 6 (triggers ModeShift -> reduces by total 3 once, NOT 3+1=4!)
        msPower = player.Creature.GetPower<ModeShiftPower>()!;
        if (msPower.Amount > 5)
            await PowerCmd.ModifyAmount(ctx, msPower, 5 - msPower.Amount, player.Creature, null);
        var evasive3 = combatState.CreateCard<EvasiveProtocol>(player);
        await CardPileCmd.Add(evasive3, PileType.Hand);
        AutoTestAssert.True(evasive3.TryManualPlay(null), "Play EvasiveProtocol #3 while Defensive AND triggering ModeShift");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<EvasiveProtocolPower>(), "EvasiveProtocolPower stacked to 3");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<WeakPower>(), "Case 4 reduced WeakPower once by 3 (4 -> 1), no double-reduce");

        // 5. GuardianWhirl (<16 Block: 2 hits = 8 each; >=16 Block: 4 hits = 16 each)
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<WeakPower>(player.Creature);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var whirl1 = combatState.CreateCard<GuardianWhirl>(player);
        var whirl2 = combatState.CreateCard<GuardianWhirl>(player);
        await CardPileCmd.Add(whirl1, PileType.Hand);
        await CardPileCmd.Add(whirl2, PileType.Hand);

        totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(whirl1.TryManualPlay(null), "Play GuardianWhirl at 0 Block (4x2 AoE = 16 total)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 16, enemy1.CurrentHp + enemy2.CurrentHp, "GuardianWhirl dealt 8 per enemy (16 total) at 0 Block");

        await CreatureCmd.GainBlock(player.Creature, 16m, ValueProp.Unpowered, null);
        totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(whirl2.TryManualPlay(null), "Play GuardianWhirl at 16 Block (4x4 AoE = 32 total)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 32, enemy1.CurrentHp + enemy2.CurrentHp, "GuardianWhirl dealt 16 per enemy (32 total) at 16+ Block");

        // 6. PrismaticBarrier (3 sockets: 2 Block per Gem + triggers Gems)
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var prism = combatState.CreateCard<PrismaticBarrier>(player);
        AutoTestAssert.Equal(3, prism.MaxSockets, "PrismaticBarrier has 3 sockets");
        AutoTestAssert.True(prism.TrySocketGem(GemType.Orange), "Socket Citrine #1");
        AutoTestAssert.True(prism.TrySocketGem(GemType.Orange), "Socket Citrine #2");
        await CardPileCmd.Add(prism, PileType.Hand);
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(prism.TryManualPlay(null), "Play PrismaticBarrier with 2 Gems");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "PrismaticBarrier granted 2x2 = 4 Block for 2 socketed Gems");
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "PrismaticBarrier triggered 2 Citrine Gems (-1 cost +2 energy)");

        // 7. Clone (copy hand card into Stasis preserving sockets; upgraded also Accelerates), Preprogram (top of draw pile into Stasis), Emergency (AccelerateAll 1x / 2x), MultiBeam (Tick +2 dmg & X-cost hits), Orbwalk (Tick +1 Str & Volatile vs upgraded non-Volatile)
        await ClearHandAndRefillEnergy(player, pcs);
        foreach (var orb in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, orb);
        await ClearHandAndRefillEnergy(player, pcs);

        var multiBeam = combatState.CreateCard<MultiBeam>(player);
        var orbwalk = combatState.CreateCard<Orbwalk>(player);
        var orbwalkUp = combatState.CreateCard<Orbwalk>(player);
        CardCmd.Upgrade(orbwalkUp);
        AutoTestAssert.True(orbwalk.IsVolatile, "Unupgraded Orbwalk is Volatile");
        AutoTestAssert.True(!orbwalkUp.IsVolatile, "Upgraded Orbwalk+ is NOT Volatile");

        var mbOrb = await GuardianMechanics.PlaceInStasisAsync(ctx, player, multiBeam);
        var owOrb = await GuardianMechanics.PlaceInStasisAsync(ctx, player, orbwalk);
        AutoTestAssert.NotNull(mbOrb, "MultiBeam placed in Stasis");
        AutoTestAssert.NotNull(owOrb, "Orbwalk placed in Stasis");

        // Emergency+ Accelerates ALL cards in Stasis twice!
        // MultiBeam (X-cost -> TurnsRemaining=1): 1st tick evokes it (+2 dmg -> 8 dmg) and returns it to Hand!
        // Orbwalk (2-cost -> TurnsRemaining=3): 2 ticks (+2 Str, turns 3->1), then unupgraded Emergency (1x) ticks 1->0 (+3 Str total, Volatile -> Exhausts)!
        var emergUp = combatState.CreateCard<Emergency>(player);
        CardCmd.Upgrade(emergUp);
        var emergBase = combatState.CreateCard<Emergency>(player);
        await CardPileCmd.Add(emergUp, PileType.Hand);
        await CardPileCmd.Add(emergBase, PileType.Hand);
        int strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(emergUp.TryManualPlay(null), "Play Emergency+ (AccelerateAll twice)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8m, multiBeam.DynamicVars.Damage.BaseValue, "MultiBeam gained +2 damage from Tick");
        AutoTestAssert.Equal(PileType.Hand, multiBeam.Pile?.Type, "MultiBeam returned to Hand on Evoke");
        AutoTestAssert.Equal(strBefore + 2, player.Creature.GetPowerAmount<StrengthPower>(), "Orbwalk granted +2 Strength across 2 Ticks");
        AutoTestAssert.Equal(1, owOrb!.TurnsRemaining, "Orbwalk has 1 turn remaining in Stasis after 2 Ticks");

        AutoTestAssert.True(emergBase.TryManualPlay(null), "Play unupgraded Emergency (AccelerateAll once)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(strBefore + 3, player.Creature.GetPowerAmount<StrengthPower>(), "Orbwalk granted 3rd Strength on 3rd Tick");
        AutoTestAssert.Equal(PileType.Exhaust, orbwalk.Pile?.Type, "Unupgraded Volatile Orbwalk Exhausted on Evoke");

        // Play MultiBeam with X=2 Energy (ensure X-cost deals 2 hits at X=2!)
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<FloatingOrbsPower>(player.Creature);
        if (pcs.Energy > 2)
            await PlayerCmd.LoseEnergy(pcs.Energy - 2, player);
        else if (pcs.Energy < 2)
            await PlayerCmd.GainEnergy(2 - pcs.Energy, player);
        totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(multiBeam.TryManualPlay(null), "Play MultiBeam at X=2 (8 dmg x 2 x 2 enemies = 32)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 32, enemy1.CurrentHp + enemy2.CurrentHp, "MultiBeam dealt 8x2 to both enemies (32 total)");

        // Test Clone+ (copies hand card preserving sockets & Accelerates) and Preprogram (top of draw pile -> Stasis + socketed Gem)
        await ClearHandAndRefillEnergy(player, pcs);
        var copyTarget = combatState.CreateCard<TwinSlam>(player);
        AutoTestAssert.True(copyTarget.TrySocketGem(GemType.Red), "Socket Ruby into TwinSlam before Clone");
        var cloneCard = combatState.CreateCard<Clone>(player);
        CardCmd.Upgrade(cloneCard);
        await CardPileCmd.Add(copyTarget, PileType.Hand);
        await CardPileCmd.Add(cloneCard, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, copyTarget)))
        {
            AutoTestAssert.True(cloneCard.TryManualPlay(null), "Play upgraded Clone+ targeting TwinSlam");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        // TwinSlam costs 1 -> TurnsRemaining starts at 2, Clone+ Accelerates it to 1!
        var clonedOrb = GuardianMechanics.GetStasisOrbs(player)
            .FirstOrDefault(o => o.StasisCard is TwinSlam ts && !ReferenceEquals(ts, copyTarget));
        AutoTestAssert.NotNull(clonedOrb, "Clone+ placed a copy of TwinSlam into Stasis");
        AutoTestAssert.Equal(1, clonedOrb!.TurnsRemaining, "Clone+ Accelerated the cloned TwinSlam from 2 -> 1 turn");
        AutoTestAssert.Equal(PileType.Hand, copyTarget.Pile?.Type, "Original TwinSlam stayed in Hand after Clone+");
        var clonedTwin = (TwinSlam)clonedOrb.StasisCard!;
        AutoTestAssert.Equal(1, clonedTwin.GetSocketedGems().Count, "Cloned TwinSlam preserved its socketed Ruby");

        // Complete Clone+ round-trip: Accelerate cloned TwinSlam into Hand and play it (verifies Ruby +2 Str + SecondStrike spawn)
        await GuardianMechanics.AccelerateAsync(ctx, player);
        AutoTestAssert.Equal(PileType.Hand, clonedTwin.Pile?.Type, "Cloned TwinSlam evoked into Hand");
        int strBeforeClonePlay = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(clonedTwin.TryManualPlay(enemy1), "Play evoked cloned TwinSlam");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(strBeforeClonePlay + 2, player.Creature.GetPowerAmount<StrengthPower>(), "Cloned TwinSlam triggered preserved Ruby (+2 Str)");
        AutoTestAssert.True(pcs.Hand.Cards.OfType<SecondStrike>().Any(), "Cloned TwinSlam spawned SecondStrike");

        // Preprogram: (a) puts a top draw-pile card into Stasis and triggers socketed Gem
        var drawTarget = combatState.CreateCard<GuardianStrike>(player);
        await CardPileCmd.Add(drawTarget, PileType.Draw, CardPilePosition.Top);
        var preprogram = combatState.CreateCard<Preprogram>(player);
        AutoTestAssert.True(preprogram.TrySocketGem(GemType.Orange), "Socket Citrine into Preprogram");
        await CardPileCmd.Add(preprogram, PileType.Hand);
        energyBefore = pcs.Energy;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, drawTarget)))
        {
            AutoTestAssert.True(preprogram.TryManualPlay(null), "Play Preprogram");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(GuardianMechanics.GetStasisOrbs(player).Any(o => ReferenceEquals(o.StasisCard, drawTarget)), "Preprogram placed top draw card into Stasis");
        AutoTestAssert.Equal(energyBefore, pcs.Energy, "Preprogram triggered socketed Citrine (-1 cost +1 energy)");

        // Preprogram (b): empty draw pile shuffles discard, 0-selection cancel puts 0 cards in Stasis and still triggers socketed Gem!
        await ClearHandAndRefillEnergy(player, pcs);
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        AutoTestAssert.Equal(0, PileType.Draw.GetPile(player).Cards.Count, "Draw pile emptied for Preprogram shuffle test");
        int stasisBeforeCancel = GuardianMechanics.GetStasisOrbs(player).Count;
        var preprogram2 = combatState.CreateCard<Preprogram>(player);
        AutoTestAssert.True(preprogram2.TrySocketGem(GemType.Orange), "Socket Citrine into Preprogram #2");
        await CardPileCmd.Add(preprogram2, PileType.Hand);
        energyBefore = pcs.Energy;
        using (DownfallAutoTestRunner.CancelCardSelection())
        {
            AutoTestAssert.True(preprogram2.TryManualPlay(null), "Play Preprogram with empty draw pile and cancel selection");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(PileType.Draw.GetPile(player).Cards.Count > 0, "Preprogram shuffled discard into draw pile");
        AutoTestAssert.Equal(stasisBeforeCancel, GuardianMechanics.GetStasisOrbs(player).Count, "Cancelled Preprogram placed 0 cards into Stasis");
        AutoTestAssert.Equal(energyBefore, pcs.Energy, "Cancelled Preprogram still triggered socketed Citrine");
    }

    internal static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheGuardian>("GUARDIAN_UNC2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

        // 1. PrismaticBeam (0-gem safe play + 3 sockets, 3 dmg per Gem + triggers Gems) & RefractedBeam (+2 unlimited upgrade: 4x6 = 24 dmg + triggers Gem 3x!)
        await ClearHandAndRefillEnergy(player, pcs);
        var pBeamEmpty = combatState.CreateCard<PrismaticBeam>(player);
        await CardPileCmd.Add(pBeamEmpty, PileType.Hand);
        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(pBeamEmpty.TryManualPlay(enemy1), "Play PrismaticBeam with 0 Gems (safe 0 dmg)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp, enemy1.CurrentHp, "0-Gem PrismaticBeam dealt 0 damage without error");

        var pBeam = combatState.CreateCard<PrismaticBeam>(player);
        AutoTestAssert.Equal(3, pBeam.MaxSockets, "PrismaticBeam has 3 sockets");
        AutoTestAssert.True(pBeam.TrySocketGem(GemType.Orange), "Socket Citrine #1 into PrismaticBeam");
        AutoTestAssert.True(pBeam.TrySocketGem(GemType.Orange), "Socket Citrine #2 into PrismaticBeam");
        await CardPileCmd.Add(pBeam, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(pBeam.TryManualPlay(enemy1), "Play PrismaticBeam with 2 Gems (3x2 = 6 dmg + 2 Citrine)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "PrismaticBeam dealt 3x2 = 6 damage");
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "PrismaticBeam triggered 2 Citrine Gems (-1 cost +2 energy)");

        var refracted = combatState.CreateCard<RefractedBeam>(player);
        CardCmd.Upgrade(refracted);
        CardCmd.Upgrade(refracted);
        AutoTestAssert.Equal(2, refracted.CurrentUpgradeLevel, "RefractedBeam upgraded twice (+2)");
        AutoTestAssert.True(refracted.TrySocketGem(GemType.Orange), "Socket Citrine into RefractedBeam+2");
        await CardPileCmd.Add(refracted, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        energyBefore = pcs.Energy;
        AutoTestAssert.True(refracted.TryManualPlay(enemy1), "Play RefractedBeam+2 (4x6 = 24 dmg + 3 Citrine triggers)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 24, enemy1.CurrentHp, "RefractedBeam+2 dealt 4x6 = 24 damage");
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "RefractedBeam+2 triggered Citrine 3x (-2 cost +3 energy)");

        // 2. VentSteam (2 sockets, 2 Vulnerable AoE + triggers Gems)
        await ClearHandAndRefillEnergy(player, pcs);
        var vent = combatState.CreateCard<VentSteam>(player);
        AutoTestAssert.Equal(2, vent.MaxSockets, "VentSteam has 2 sockets");
        AutoTestAssert.True(vent.TrySocketGem(GemType.Orange), "Socket Citrine into VentSteam");
        await CardPileCmd.Add(vent, PileType.Hand);
        energyBefore = pcs.Energy;
        AutoTestAssert.True(vent.TryManualPlay(null), "Play VentSteam");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, enemy1.GetPowerAmount<VulnerablePower>(), "VentSteam applied 2 Vulnerable to enemy1");
        AutoTestAssert.Equal(2, enemy2.GetPowerAmount<VulnerablePower>(), "VentSteam applied 2 Vulnerable to enemy2");
        AutoTestAssert.Equal(energyBefore, pcs.Energy, "VentSteam triggered socketed Citrine");
        await PowerCmd.Remove<VulnerablePower>(enemy1);
        await PowerCmd.Remove<VulnerablePower>(enemy2);

        // 3. Recover (empty discard pile still grants Block + Brace; non-empty discard places chosen card into Stasis)
        await ClearHandAndRefillEnergy(player, pcs);
        foreach (var c in PileType.Discard.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Draw);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int msBefore = player.Creature.GetPowerAmount<ModeShiftPower>();
        var recoverEmpty = combatState.CreateCard<Recover>(player);
        await CardPileCmd.Add(recoverEmpty, PileType.Hand);
        AutoTestAssert.True(recoverEmpty.TryManualPlay(null), "Play Recover with empty discard pile");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.Block, "Recover with empty discard still granted 5 Block");
        AutoTestAssert.Equal(msBefore - 3, player.Creature.GetPowerAmount<ModeShiftPower>(), "Recover with empty discard still Braced 3");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var discardTarget = combatState.CreateCard<GuardianStrike>(player);
        await CardPileCmd.Add(discardTarget, PileType.Discard);
        msBefore = player.Creature.GetPowerAmount<ModeShiftPower>();
        var recover = combatState.CreateCard<Recover>(player);
        await CardPileCmd.Add(recover, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, discardTarget)))
        {
            AutoTestAssert.True(recover.TryManualPlay(null), "Play Recover targeting discard Strike");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(5, player.Creature.Block, "Recover granted 5 Block");
        AutoTestAssert.Equal(msBefore - 3, player.Creature.GetPowerAmount<ModeShiftPower>(), "Recover Braced 3");
        AutoTestAssert.True(GuardianMechanics.GetStasisOrbs(player).Any(o => ReferenceEquals(o.StasisCard, discardTarget)), "Recover placed chosen Discard card into Stasis");

        // 4. SentryBeam+ -> SentryWave+ in Stasis -> Evoke -> SentryWave+ (1 Weak + Brace 2 + SentryBeam+ in Stasis!)
        await ClearHandAndRefillEnergy(player, pcs);
        foreach (var orb in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, orb);
        await ClearHandAndRefillEnergy(player, pcs);

        var sentryBeamUp = combatState.CreateCard<SentryBeam>(player);
        CardCmd.Upgrade(sentryBeamUp);
        await CardPileCmd.Add(sentryBeamUp, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(sentryBeamUp.TryManualPlay(enemy1), "Play SentryBeam+ (7 dmg + SentryWave+ in Stasis + Exhaust)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 7, enemy1.CurrentHp, "SentryBeam+ dealt 7 damage");
        AutoTestAssert.Equal(PileType.Exhaust, sentryBeamUp.Pile?.Type, "SentryBeam+ Exhausted");
        var waveOrb = GuardianMechanics.GetStasisOrbs(player).FirstOrDefault(o => o.StasisCard is SentryWave);
        AutoTestAssert.NotNull(waveOrb, "SentryBeam+ placed SentryWave into Stasis");
        var wave = (SentryWave)waveOrb!.StasisCard!;
        AutoTestAssert.True(wave.IsUpgraded, "Spawned SentryWave is upgraded");

        // Accelerate 1x -> SentryWave+ (0-cost -> TurnsRemaining=1) evokes to Hand!
        await GuardianMechanics.AccelerateAsync(ctx, player);
        AutoTestAssert.Equal(PileType.Hand, wave.Pile?.Type, "SentryWave+ evoked into Hand");
        msBefore = player.Creature.GetPowerAmount<ModeShiftPower>();
        AutoTestAssert.True(wave.TryManualPlay(enemy1), "Play SentryWave+ (1 Weak + Brace 2 + SentryBeam+ in Stasis)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "SentryWave+ applied 1 Weak");
        AutoTestAssert.Equal(msBefore - 2, player.Creature.GetPowerAmount<ModeShiftPower>(), "SentryWave+ Braced 2");
        AutoTestAssert.Equal(PileType.Exhaust, wave.Pile?.Type, "SentryWave+ Exhausted");
        var spawnedBeamOrb = GuardianMechanics.GetStasisOrbs(player).FirstOrDefault(o => o.StasisCard is SentryBeam);
        AutoTestAssert.NotNull(spawnedBeamOrb, "SentryWave+ placed SentryBeam+ into Stasis");
        AutoTestAssert.True(spawnedBeamOrb!.StasisCard!.IsUpgraded, "Spawned SentryBeam is upgraded");

        // 5. ShieldSpikes (outside Defensive Mode: no Thorns; inside Defensive Mode: +3 Thorns) & SpikerProtocol (all cases)
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<DefensiveModePower>(player.Creature);
        await PowerCmd.Remove<ThornsPower>(player.Creature);
        var msPower = player.Creature.GetPower<ModeShiftPower>()!;
        if (msPower.Amount < 20)
            await PowerCmd.ModifyAmount(ctx, msPower, 20 - msPower.Amount, player.Creature, null);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var ss1 = combatState.CreateCard<ShieldSpikes>(player);
        await CardPileCmd.Add(ss1, PileType.Hand);
        AutoTestAssert.True(ss1.TryManualPlay(null), "Play ShieldSpikes outside Defensive Mode");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.Block, "ShieldSpikes granted 12 Block");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ThornsPower>(), "ShieldSpikes did not grant Thorns outside Defensive Mode");

        // SpikerProtocol Case 1: outside Defensive Mode & ModeShift > 6 -> 0 Thorns immediately, Power(2) applied
        msPower = player.Creature.GetPower<ModeShiftPower>()!;
        if (msPower.Amount < 20)
            await PowerCmd.ModifyAmount(ctx, msPower, 20 - msPower.Amount, player.Creature, null);
        var sp0 = combatState.CreateCard<SpikerProtocol>(player);
        await CardPileCmd.Add(sp0, PileType.Hand);
        AutoTestAssert.True(sp0.TryManualPlay(null), "Play SpikerProtocol Case 1 (outside Defensive Mode, no ModeShift)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ThornsPower>(), "SpikerProtocol Case 1 granted 0 Thorns immediately");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<SpikerProtocolPower>(), "SpikerProtocolPower(2) applied");

        // SpikerProtocol Case 2: triggers ModeShift (3 DefMode + 4 SpikerProtocol = 7 Thorns!)
        msPower = player.Creature.GetPower<ModeShiftPower>()!;
        if (msPower.Amount > 5)
            await PowerCmd.ModifyAmount(ctx, msPower, 5 - msPower.Amount, player.Creature, null);
        var sp1 = combatState.CreateCard<SpikerProtocol>(player);
        await CardPileCmd.Add(sp1, PileType.Hand);
        AutoTestAssert.True(sp1.TryManualPlay(null), "Play SpikerProtocol Case 2 (triggers ModeShift)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<ThornsPower>(), "SpikerProtocol Case 2 triggered ModeShift (3 DefMode + 4 Spiker = 7 Thorns)");

        // ShieldSpikes inside Defensive Mode (+3 Thorns -> 10!)
        var ss2 = combatState.CreateCard<ShieldSpikes>(player);
        await CardPileCmd.Add(ss2, PileType.Hand);
        AutoTestAssert.True(ss2.TryManualPlay(null), "Play ShieldSpikes inside Defensive Mode");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<ThornsPower>(), "ShieldSpikes granted +3 Thorns inside Defensive Mode (7 -> 10)");

        // SpikerProtocol Case 3: already in Defensive Mode without ModeShift (+2 Thorns -> 12!)
        var sp2 = combatState.CreateCard<SpikerProtocol>(player);
        await CardPileCmd.Add(sp2, PileType.Hand);
        AutoTestAssert.True(sp2.TryManualPlay(null), "Play SpikerProtocol Case 3 (already Defensive, no ModeShift)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<ThornsPower>(), "SpikerProtocol Case 3 granted +2 Thorns immediately (10 -> 12)");

        // SpikerProtocol Case 4: already in Defensive Mode AND ModeShift <= 6 (triggers ModeShift -> +8 Spiker once, NOT 8+2=10!)
        msPower = player.Creature.GetPower<ModeShiftPower>()!;
        if (msPower.Amount > 5)
            await PowerCmd.ModifyAmount(ctx, msPower, 5 - msPower.Amount, player.Creature, null);
        var sp3 = combatState.CreateCard<SpikerProtocol>(player);
        await CardPileCmd.Add(sp3, PileType.Hand);
        AutoTestAssert.True(sp3.TryManualPlay(null), "Play SpikerProtocol Case 4 (already Defensive AND triggers ModeShift)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<SpikerProtocolPower>(), "SpikerProtocolPower stacked to 8");
        AutoTestAssert.Equal(20, player.Creature.GetPowerAmount<ThornsPower>(), "SpikerProtocol Case 4 granted +8 once (12 -> 20), no double-grant");

        // 6. StasisStrike (16 dmg + +1 Slot), TimeCapacitor (+1 Slot), TimeBomb (14 AoE + AccelerateAll + Exhaust)
        await ClearHandAndRefillEnergy(player, pcs);
        int slotsBefore = pcs.OrbQueue.Capacity;
        var stStrike = combatState.CreateCard<StasisStrike>(player);
        var tCap = combatState.CreateCard<TimeCapacitor>(player);
        var tBomb = combatState.CreateCard<TimeBomb>(player);
        await CardPileCmd.Add(stStrike, PileType.Hand);
        await CardPileCmd.Add(tCap, PileType.Hand);
        await CardPileCmd.Add(tBomb, PileType.Hand);

        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(stStrike.TryManualPlay(enemy1), "Play StasisStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 16, enemy1.CurrentHp, "StasisStrike dealt 16 damage");
        AutoTestAssert.Equal(slotsBefore + 1, pcs.OrbQueue.Capacity, "StasisStrike added +1 Stasis Slot");

        AutoTestAssert.True(tCap.TryManualPlay(null), "Play TimeCapacitor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(slotsBefore + 2, pcs.OrbQueue.Capacity, "TimeCapacitor added +1 Stasis Slot");

        // Put a 2-cost card (TurnsRemaining=3) in Stasis and play TimeBomb -> ticks 3->2 + 14 AoE + Exhaust!
        foreach (var orb in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, orb);
        await ClearHandAndRefillEnergy(player, pcs);
        await CardPileCmd.Add(tBomb, PileType.Hand);
        var bombWaiter = combatState.CreateCard<RollAttack>(player); // cost 2 -> 3 turns
        var bombOrb = await GuardianMechanics.PlaceInStasisAsync(ctx, player, bombWaiter);
        AutoTestAssert.Equal(3, bombOrb!.TurnsRemaining, "RollAttack entered Stasis with 3 turns");
        int totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(tBomb.TryManualPlay(null), "Play TimeBomb");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 28, enemy1.CurrentHp + enemy2.CurrentHp, "TimeBomb dealt 14 AoE (28 total)");
        AutoTestAssert.Equal(2, bombOrb.TurnsRemaining, "TimeBomb Accelerated RollAttack 3 -> 2 turns");
        AutoTestAssert.Equal(PileType.Exhaust, tBomb.Pile?.Type, "TimeBomb Exhausted");

        // 7. Repulse (ExhaustStatusesPower: first Status drawn is Exhausted + draws replacement; second stays) & TimeSifter (AutomayhemPower: accelerates oldest orb on Round 2!)
        await ClearHandAndRefillEnergy(player, pcs);
        var repulse = combatState.CreateCard<Repulse>(player);
        var timeSifter = combatState.CreateCard<TimeSifter>(player);
        await CardPileCmd.Add(repulse, PileType.Hand);
        await CardPileCmd.Add(timeSifter, PileType.Hand);
        AutoTestAssert.True(repulse.TryManualPlay(null), "Play Repulse");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ExhaustStatusesPower>(), "Repulse applied ExhaustStatusesPower(1)");

        AutoTestAssert.True(timeSifter.TryManualPlay(null), "Play TimeSifter");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<AutomayhemPower>(), "TimeSifter applied AutomayhemPower(1)");

        // Stack draw pile top: Dazed (1st Status -> Exhausted + draw!), Dazed (2nd Status -> stays in Hand!), GuardianDefend (drawn as replacement!)
        var status1 = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Dazed>(player);
        var status2 = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Dazed>(player);
        var cleanCard = combatState.CreateCard<GuardianDefend>(player);
        await CardPileCmd.Add(cleanCard, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(status2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(status1, PileType.Draw, CardPilePosition.Top);

        await CardPileCmd.Draw(ctx, 1, player);
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(PileType.Exhaust, status1.Pile?.Type, "Repulse Exhausted 1st drawn Status");
        AutoTestAssert.Equal(PileType.Hand, status2.Pile?.Type, "Repulse left 2nd drawn Status in Hand (only 1 per turn)");

        // End Round 1 -> Round 2:
        // Verify AutomayhemPower runs AFTER OrbQueue.AfterTurnStart with TWO Stasis orbs:
        // Put Orb A (0-cost -> 1 turn) BEFORE Orb B (bombOrb at 2 turns)!
        //Wait: bombOrb is already Orb 0 (2 turns); clear and place Orb A (1 turn) FIRST, then Orb B (2 turns) SECOND!
        foreach (var orb in GuardianMechanics.GetStasisOrbs(player))
            await GuardianMechanics.EvokeSpecificOrbAsync(ctx, player, orb);
        await ClearHandAndRefillEnergy(player, pcs);

        var orbACard = combatState.CreateCard<CrystalShiv>(player); // 0-cost -> 1 turn
        var orbBCard = combatState.CreateCard<GuardianStrike>(player); // 1-cost -> 2 turns
        var orbA = await GuardianMechanics.PlaceInStasisAsync(ctx, player, orbACard);
        var orbB = await GuardianMechanics.PlaceInStasisAsync(ctx, player, orbBCard);
        AutoTestAssert.Equal(1, orbA!.TurnsRemaining, "Orb A (oldest) starts at 1 turn");
        AutoTestAssert.Equal(2, orbB!.TurnsRemaining, "Orb B (second) starts at 2 turns");

        // Also stack a Curse on top of Draw Pile for Round 2 to verify ExhaustStatusesPower resets per turn AND works on Curses!
        var curseCard = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Clumsy>(player);
        await CardPileCmd.Add(curseCard, PileType.Draw, CardPilePosition.Top);

        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);

        // On Round 2:
        // 1) ExhaustStatusesPower reset in BeforeSideTurnStart -> exhausted drawn Curse!
        AutoTestAssert.Equal(PileType.Exhaust, curseCard.Pile?.Type, "Repulse reset on Round 2 and Exhausted drawn Curse");
        // 2) OrbQueue.AfterTurnStart ticks Orb A (1->0, evokes) & Orb B (2->1), THEN AutomayhemPower accelerates Orb B (1->0, evokes)!
        AutoTestAssert.Equal(PileType.Hand, orbACard.Pile?.Type, "Orb A naturally evoked on Round 2");
        AutoTestAssert.Equal(PileType.Hand, orbBCard.Pile?.Type, "TimeSifter ran AFTER natural orb ticks and evoked Orb B too on Round 2");
    }

    internal static async Task ClearHandAndRefillEnergy(Player player, PlayerCombatState pcs)
    {
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        if (pcs.Energy < 10)
            await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
    }
}
