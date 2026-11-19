# Phase 6 Plan — Core Potions + Packs #9 & #10 (100+ Draftable Cards)

## Goals
1. **Complete Packmaster's core character identity** by porting its 3 core character potions (`ModdersDelight`, `BoosterBrew`, `SmithingOil`) into `PackmasterPotionPool`.
2. **Pass the 100-draftable-card milestone** by porting 2 clean, iconic, zero-patch packs:
   - **Pack #9 (`WardenPack`)** — 10th total pack (100 draftable cards): Scry + draw-pile manipulation + next-turn/predict payoffs.
   - **Pack #10 (`FarmerPack`)** — 11th total pack (110 draftable cards): Card-type sequencing + `Fertilizer` Status token engine.
3. Keep complexity strictly minimal: **zero new Harmony patches**, native StS 2 / BaseLib hooks only, and full headless autotest coverage for every potion, card, and power.

---

## Phase 6A: Core Packmaster Potions (`Packmaster/Potions/`)

### Pool & Registration
- Base class `PackmasterPotion : CustomPotionModel` decorated with `[Pool(typeof(PackmasterPotionPool))]`, supplying `CustomPackedImagePath` (`res://Packmaster/images/potions/{IconKey}.png`) and `CustomPackedOutlinePath` (`res://Packmaster/images/potions/{IconKey}_outline.png`).
- Verify `BaseLib`'s `[Pool(typeof(PackmasterPotionPool))]` + `CustomPotionModel` image/outline path resolution in `TestModelDbAssetsAndLocIntegrity` (validate every potion in `PackmasterPotionPool` has valid `Image`, `Outline`, `Title`, and `DynamicDescription` with no unresolved SmartFormat tokens).
- Generate crisp StS-1-derived potion bottle PNGs (`94x94` + `_outline.png`, matching StS 2's potion icon dimensions — verify exact dimensions in `sts2.dll` / existing sprites) from `anniv5Resources/images/vfx/womaninbluepack/` tinted to Packmaster's authentic tan/gold/white palette.

### Potions (3)
*(Note: StS 1's `PackInAJar` is an event-only `PLACEHOLDER` rarity potion from `BlackMarketDealerEvent`, not in the regular potion pool — deferred until Packmaster events are ported. Verified in `sts2.dll`: all vanilla StS 2 player-targeted combat potions including `GamblersBrew`, `ColorlessPotion`, `LiquidMemories`, `SwiftPotion`, and `BlessingOfTheForge` use `TargetType.AnyPlayer` + `PotionModel.AssertValidForTargetedPotion(target)`.)*

1. **`ModdersDelight`** (`Common`, `CombatOnly`, `TargetType.AnyPlayer`, `CardsVar(3)`)
   - If `DiscardPile` is non-empty, calls `await CardPileCmd.Shuffle(choiceContext, player)` (which in StS 2 natively moves `DiscardPile` into `DrawPile` and shuffles them together).
   - Calls unified `PackHelper.FetchDistinctFromDrawAsync<TKey>(player, DynamicVars.Cards.IntValue, PackRegistry.GetForCard)` (shared by `BoosterTutor`, `ModdersDelight`, and `SowTheSeeds`).
2. **`BoosterBrew`** (`Uncommon`, `CombatOnly`, `TargetType.AnyPlayer`, `CardsVar(5)`)
   - Picks 3 distinct random registered packs (using `player.RunState.Rng.CombatCardGeneration`).
   - Presents 1 representative canonical card (`CanBeGeneratedInCombat`, fallback all pack cards) from each of the 3 packs via `CardSelectCmd.FromChooseACardScreen(choiceContext, previewCards, player, canSkip: false)`, mapping the picked card back via `PackRegistry.GetForCard(picked)`.
   - Adds `DynamicVars.Cards.IntValue` random `CanBeGeneratedInCombat` cards from the chosen pack into `DrawPile` (`CardPilePosition.Random`), setting `EnergyCost.SetThisCombat(0)` only when `!card.EnergyCost.CostsX && card.EnergyCost.Canonical > 0`.
3. **`SmithingOil`** (`Rare`, `CombatOnly`, `TargetType.AnyPlayer`, `CardsVar(1)`)
   - Collects eligible cards in `Hand` (`c.IsUpgradable || (c.DeckVersion?.IsUpgradable ?? false)`), returning early if none; clamps selection count to `Math.Min(DynamicVars.Cards.IntValue, eligible.Count)` so Sacred Bark (2) works cleanly.
   - Upgrades each selected card in combat (`if (c.IsUpgradable) CardCmd.Upgrade(c)`) AND permanently in deck (`if (c.DeckVersion is { IsUpgradable: true } deckCard) CardCmd.Upgrade(deckCard)`).

### Tests (6A)
- Extend `TestModelDbAssetsAndLocIntegrity` to validate `PackmasterPotionPool` potions (`Title`, `DynamicDescription`, `Image`, `Outline`).
- Add `TestCorePackmasterPotions` exercising all 3 potions in combat:
  - `ModdersDelight`: shuffles discard into draw and pulls up to 3 cards from distinct packs into hand (respecting hand limit and distinct-pack rule).
  - `BoosterBrew`: selects 1 of 3 pack choices via deterministic test selector and shuffles 5 cards from that pack into draw pile costing 0 this combat (preserving 0-cost/X-cost invariants).
  - `SmithingOil`: upgrades a combat card in hand AND verifies `player.Deck.Cards` permanently upgraded its `DeckVersion` (including an already-combat-upgraded card whose `DeckVersion` is still unupgraded!).

---

## Phase 6B: Pack #9 — `WardenPack` (10th Total Pack / 100 Draftable Cards)

### Mechanics & Reuse
- Uses BaseLib's native `ScryVar` + `ScryCmd.Execute(choiceContext, this)` (already proven in `ShamanPack`).
- Uses StS 2's built-in `BlockNextTurnPower`, `VulnerablePower`, `StrengthPower`, `FlexPotionPower` (for `DivineMight`'s temporary +10 Strength this turn), `MonsterModel.IntendsToAttack`, and `CardCmd.AutoPlay`.
- **2 small powers (`Packmaster/Powers/`):**
  - `EnigmancyPower`: at `AfterEnergyReset` for `Owner.Player`, gain `Amount` Energy, then decrement `Amount` by `1` (removes itself when `Amount` reaches `0`).
  - `DivineMightPower`: dead-simple `PowerStackType.Single` marker power — `DivineMight.OnPlay` checks `Owner.Creature.HasPower<DivineMightPower>()` and either removes it + Ascends (+2 Energy, draw 2, +10 temp Strength) or applies `DivineMightPower`.
- **10 draftable cards (`Packmaster/Cards/Warden/`):**
  1. `PierceVeil` (Common Attack): deal damage, look at top N cards of Draw Pile (index `0..N-1`) and choose 1 to put into Hand.
  2. `Stall` (Common Skill): gain Block, put up to N cards from Hand on top of Draw Pile (`CardPilePosition.Top`).
  3. `RiskOfBlades` (Common Attack): deal damage once (or 3 times if top card of Draw Pile is an Attack; glows gold when true).
  4. `Crossroads` (Uncommon Skill, Exhaust): Scry N, `CardPileCmd.ShuffleIfNecessary`, then auto-play the top card of Draw Pile (`CardCmd.AutoPlay`).
  5. `Anticipation` (Uncommon Skill): draw 2 cards (track exact drawn cards via `CardPileCmd.Draw` / `CardDrawnEntry`); gain Strength equal to max (upgraded: sum) of their logical costs (0 for unplayable, current Energy for X-cost).
  6. `Prediction` (Uncommon Skill): compute Dexterity/Frail-modified Block; if no living enemy intends to attack, apply `BlockNextTurnPower(modifiedBlock)` instead of `GainBlock`; glows gold when no enemy intends to attack.
  7. `ForceBolt` (Uncommon Attack): deal damage + apply Vulnerable; upgraded has Persist 2 (stateless count of `CardPlaysStarted` entries for this card this turn inside `ModifyCardPlayResultLocation` — zero mutable fields, clone-safe for `Recurrence`).
  8. `Enigmancy` (Rare Power): Scry N + apply `EnigmancyPower`.
  9. `Recurrence` (Rare Skill, Exhaust): shuffle a clone of every other card in Hand into Draw Pile.
  10. `DivineMight` (Rare Attack): deal damage; first play applies `DivineMightPower`, second play removes it and Ascends (+2 Energy, draw 2, +10 temp Strength).

---

## Phase 6C: Pack #10 — `FarmerPack` (11th Total Pack / 110 Draftable Cards)

### Mechanics & Reuse
- Small stateless helper `FarmerHelper` querying `CombatManager.Instance.History.CardPlaysStarted` (NOT `CardPlaysFinished`, so nested `AutoPlay` and `OutToMarketPower.AfterCardPlayed` both work reliably):
  - `GetDistinctCardTypesPlayedThisTurn(Player?, ICombatState?, CardPlay? excludeCurrentPlay = null)`
  - `GetPreviousCardTypePlayedThisCombat(Player?, CardPlay? currentPlay = null)`
- **Token card:** `Fertilizer` (0 cost Status token, Exhaust, draw 1 (+1 -> 2), upgradable).
- **2 small powers (`Packmaster/Powers/`):**
  - `OutToMarketPower`: in `AfterCardPlayed`, if previous card type exists and differs from `cardPlay.Card.Type`, deal `Amount` `ValueProp.Unpowered` damage to ALL enemies.
  - `SupplyChainPower`: at `AfterPlayerTurnStart`, add `Amount` `Fertilizer` to Hand.
- **10 draftable cards (`Packmaster/Cards/Farmer/`):**
  `HarvestBeans`, `HarvestCarrots`, `HarvestPotatoes`, `HarvestRadishes`, `OutToMarket`, `PestControl`, `SoilResiliency` (Exhaustive 3 -> 4), `SupplyChain`, `SowTheSeeds` (reuses `PackHelper.FetchDistinctFromDrawAsync`), `TillTheEarth`.

---

## Execution & Quality Gates
- Implement **6A -> review -> test -> commit**, then **6B -> review -> test -> commit**, then **6C -> review -> test -> commit**.
- Every commit requires:
  1. High-model `reviewer` subagent review (`Model: "inherit"`) for simplicity, minimal complexity, and StS 2 / BaseLib correctness.
  2. `./scripts/test.sh --pack` (when PNGs/JSONs change) passing with 0 warnings, 0 errors, 100% pass rate.
