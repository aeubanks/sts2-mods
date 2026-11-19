# Packmaster StS2 — Porting Plan

## Guiding Principles
1. **Simplicity first:** Use native StS 2 + BaseLib APIs wherever possible (`PlaceholderCharacterModel` → `CustomCharacterModel` in Phase 5, `CustomCardModel`, `CustomRelicModel`, `CustomPowerModel`, `CardPoolModel.FilterThroughEpochs`, BaseLib `ExtendedSaveHandlers` or minimal run-scoped save serialization, `SimpleModConfig`). Avoid Harmony patches unless no virtual override or hook exists.
2. **Vertical slices, always playable & tested:** Every milestone compiles, packs, passes automated headless tests (`scripts/test.sh`), gets a subagent simplicity review, and is committed cleanly.
3. **Reuse StS 1 assets & strings:** Copy card art (`250x190` + `500x380` `_p`), relic/power/pack art, and English text directly from `~/repos/PackmasterCharacter` (and later `~/repos/Packmaster-Expansion-Packs`), adapting descriptions to StS 2 SmartFormat syntax (`{Damage:diff()}`, `[gold]...[/gold]`, `{energy:energyIcons()}`).
4. **Defer complex subsystems until the core loop is rock-solid:** Port packs that map cleanly onto StS 2's native mechanics (damage, block, draw, energy, exhaust, discard, poison, strength, powers, claws) BEFORE building custom Stance/Orb/Hat subsystems.

---

## Phased Milestones

### Phase 0: Automated Headless Test Harness
- Implement the plan in `docs/TESTING_PLAN.md`: `scripts/test.sh` + `Packmaster/Testing/` running real headless `SlayTheSpire2 --headless --force-steam=off` tests.
- Keep current placeholder character passing all integrity + combat tests.

### Phase 1: Authentic Starter Kit (Character + Relic + Starter Deck)
Replace the temporary 2-card starting deck & placeholder Burning Blood with Packmaster's real starter kit from StS 1:
- **Starting Deck (10 cards):** 4× `PackmasterStrike`, 4× `PackmasterDefend`, 1× `Rummage`, 1× `Cardistry`.
  - `Rummage` (2-cost Attack: 10(14) dmg, draw 1 card and reduce its cost by 1 this turn via `EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true)`, guarding X-cost/unplayable cards).
  - `Cardistry` (0-cost Skill: 2(4) Block, apply native StS 2 `ReboundPower` if present, or minimal custom equivalent).
- **Starting Relic:** `HandyHaversack` (`CustomRelicModel` matching vanilla `BagOfPreparation` + `Lantern` round-1 hooks: draw 1 extra card + gain 1 energy on turn 1) with StS 1 relic art.
- **Guardrail:** Make `PackmasterCard.PortraitKey` explicit/abstract so new cards cannot accidentally inherit `"strike"` art.
- **Automated Tests:** Verify starting deck composition, `HandyHaversack` turn-1 draw+energy, `Rummage` cost reduction, and `Cardistry` top-of-deck effect in real headless combat.

### Phase 2: Pack System, Card Pool Filtering, Synergy & Config
Build the minimal, single-source-of-truth core of Packmaster's pack mechanic:
- **Prerequisite verification in `sts2.dll` / `BaseLib.dll`:**
  - Verify whether all card reward, shop, transform, and combat card-generation callers route through `CardPoolModel.GetUnlockedCards` / `FilterThroughEpochs` (and whether any cache results per `UnlockState`).
  - Verify whether BaseLib `ExtendedSaveHandlers` supports `RunState`/`Player` (note: `SavedSpireField` only works on Cards/Relics, not `RunState`/`Player`); persist active packs as a single comma-delimited string (e.g. `"CoreSet,Basics,Claw"`) on the run save.
- **`PackDefinition` + `PackRegistry` (single source of truth):**
  - `PackDefinition(string Id, bool AlwaysInclude, IReadOnlyList<Type> CardTypes)` defined in one place; reverse `Type -> PackDefinition` lookup built automatically.
- **Run-Start Pack Assignment (before Neow generates Floor-0 options):**
  - Deterministically roll `CoreSetPack` + `(PacksPerRun - 1)` random packs using the run's RNG seed as soon as a Packmaster `RunState` initializes (before Act 1 / Neow room entry).
- **Card Pool Filtering:**
  - Override `PackmasterCardPool.FilterThroughEpochs(...)` (plus minimal hooks only if any caller bypasses it) to include Basic cards + cards in the active run's packs; return all cards outside a run (Compendium) or when `AllPacksMode` is enabled.
- **Pack Name Display & `HasSynergy()`:**
  - Show each card's pack name via BaseLib (`ICustomTypeTextCard` or hover tip — zero custom Godot UI).
  - Implement shared `PackHelper.HasSynergy(CardModel card)` (`>= 2` *other* distinct packs present among cards in hand besides `card`'s own pack).
- **`PackmasterConfig` (`SimpleModConfig`):**
  - Replace `PlaceholderOption` with `AllPacksMode` (`bool`, default `false`) and `PacksPerRun` (`int`, `[ConfigSlider(3, 10)]`, `[ConfigVisibleIf(nameof(AllPacksMode), Invert = true)]`, default `7`).
- **Automated Tests:** Verify pool filtering across rewards, shop, transform, and combat generation; verify `AllPacksMode`; verify seed determinism, save/load persistence, and `HasSynergy()`.

### Phase 3: Core Set Pack (`CoreSetPack`, 10 cards)
Port all 10 cards of `CoreSetPack` (always in every Packmaster run), removing the temporary `CommonStrike`/`UncommonStrike`/`RareStrike` placeholders once Core Set provides Common/Uncommon/Rare cards:
- `BackpackSmack` (X-cost via native `CostsX`/`ResolveEnergyXValue()`, `HasSynergy()`), `BoosterTutor` (guard `< N` distinct packs gracefully), `DestinyDraw` (guard `< 3` packs gracefully), `Flick` (handle empty hand & X-cost/unplayable cards safely), `MayhemForm` (guard `MayhemFormPower` against firing mid-resolution or during end-of-turn discard), `RippedPecs` (`HasSynergy()`), `Showoff`, `Sideboard`, `SmithingHammer`, `Synergize` (`HasSynergy()`).
- **Automated Tests:** Play every single Core Set card in headless combat (using lightweight test-only packs or initial Phase 4 packs to cover `HasSynergy()` and multi-pack selection).

### Phase 4: First Wave of Native-Mechanic Packs (Target: ≥ 7 Total Packs for a Full Authentic Run)
Port 6+ self-contained packs that map cleanly to StS 2 mechanics (each pack = 1 focused commit + simplicity review + headless tests):
1. **`BasicsPack`** (10 cards) — Strike/Defend synergy, simple damage/block/draw/cost mechanics.
2. **`ClawPack`** (10–11 cards) — Iconic Claw scaling pack (StS 2 natively supports Claw-style scaling).
3. **5+ additional StS2-native packs** selected from `PackmasterCharacter` whose mechanics map directly to native StS 2 powers/commands (Strength, Poison, Vulnerable, Weak, Exhaust, Discard, Energy, Draw — e.g. `StrikesPack`, `PoisonPack`, `HighEnergyPack`, `IroncladPack`, `SilentPack`, `WarlockPack`, `BitingColdPack`, `AggressionPack` without Stances or whichever are cleanest).

### Phase 5: Interactive Pack Draft, Relics, Potions & Visual Polish
- **Interactive Pack Draft at Run Start:** Run the 3 pack picks (StS 1 default: Core Set + 3 Random + 3 Choice) as pre-blessing option pages in Neow (`AncientEventModel` / `EventOption` with title, description, and hover tips) — **zero fake Pack Preview `CardModel`s and zero custom Godot UI**, with native save/resume, controller, and multiplayer support.
- Core Packmaster Relics (`PMCollection`, `BagOfHolding`, `GemOfUnity`, etc.) and Packmaster Potions (`BoosterBrew`, `SmithingOil`, etc.).
- Character visual polish (transition from `PlaceholderCharacterModel` to custom Packmaster combat visuals/animations and layered energy orb).

### Phase 6: Expansion Packs & Advanced Subsystems (Future)
- Port additional packs from `PackmasterCharacter` and `Packmaster-Expansion-Packs`.
- Add shared subsystems only when actually required by packs (e.g. Stances, Orbs, Card Modifiers, Packmaster Hats).

---

## Open Questions Saved for User (Proceeding With Sensible Defaults Meanwhile)
1. **Temporary placeholder Strikes:** Keep `CommonStrike`/`UncommonStrike`/`RareStrike` until `CoreSetPack` (Phase 3) provides real Common/Uncommon/Rare cards, then delete them.
2. **StS 2 balance / mechanics differences:** Map to native StS 2 equivalents where they exist (`ReboundPower`, `PoisonPower`, `StrengthPower`, `VulnerablePower`, etc.) and defer StS1-only subsystems (Stances, Orbs, Hats) until Phase 6.
3. **Pack Draft UI:** Defaulting to native Neow / `AncientEventModel` multi-page option choices (zero fake CardModels, zero custom Godot UI).
4. **Local `mods/BaseLib` duplicate:** Keeping local `mods/BaseLib` is required for `--force-steam=off` headless automated testing.
