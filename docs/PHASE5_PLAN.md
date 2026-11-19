# Phase 5 Plan — Run-Save Persistence, Interactive Pack Draft, Core Relics, & Expanding to 9+ Packs

With 7 full packs ported and passing 10/10 headless autotests (`CoreSet`, `Quiet`, `BitingCold`, `Warlock`, `EvenOdd`, `HighEnergy`, `Batter` = 70 draftable cards + tokens + 20 powers), Phase 5 completes the authentic Packmaster run loop while staying as simple as possible.

---

## Guiding Principle: Minimal Complexity
- Reuse **BaseLib** `ExtendedSaveTypes.RegisterSavedValue<IRunState, string>` directly for run-save persistence (single source of truth, zero duplicate caches, zero custom JSON/save Harmony patches).
- Reuse **StS 2's native `Neow` (`AncientEventModel`) `EventOption` pages** for Interactive Pack Draft (zero custom Godot UI scenes, zero fake preview `CardModel`s).
- Keep each sub-phase as its own small, independently tested and reviewed commit.

---

## Phase 5A — Run-Save Persistence for Active Packs
- Update `Packmaster/Packs/PackRunState.cs`:
  - Keep ONE `ConditionalWeakTable<IRunState, HashSet<string>>` (`_persistedByRun`) for finalized/persisted packs (never populated by lazy seed-fallback!).
  - Register `_persistedByRun` unconditionally inside `MainFile.Initialize()` (via `PackRunState.Initialize()`) using BaseLib's `ExtendedSaveTypes.RegisterSavedValue<IRunState, string>("packmaster_active_packs", ...)`:
    - `getter`: returns `string.Join(",", set)` when `_persistedByRun.TryGetValue(runState, out var set)` else `null`.
    - `setter`: when `!string.IsNullOrEmpty(val)`, writes parsed `HashSet<string>` into `_persistedByRun`.
    - `serializer` / `deserializer`: `PacketWriter.WriteString` / `PacketReader.ReadString`.
  - When `GetActivePackIds(IRunState state)` runs:
    - Return `_persistedByRun` if finalized (i.e. persisted from Neow draft, save load, or explicit `SetActivePacksForRun`).
    - Otherwise return a deterministic seed-rolled fallback from `RollPackIdsForSeed(...)` **without** writing to `_persistedByRun` (so calling `GetActivePackIds` before Neow draft never blocks the draft or pollutes save data).
  - Add `HasPersistedActivePacks(IRunState state)` for Phase 5B.
- **Autotest (`PackRunStateSaveLoadPersistence`):**
  - Start a Packmaster run, call `PackRunState.SetActivePacksForRun(runState, ["CoreSet", "Quiet", "Batter"])`, serialize via `RunManager.Instance.ToSave(null)`, round-trip `SerializableRun` through StS 2's actual `JsonSerializer.Serialize` / `JsonSerializer.Deserialize` (using `JsonSerializationUtility.GetTypeInfo<SerializableRun>()`) AND `RunState.FromSerializable(save)`, and assert `PackRunState.GetActivePackIds(loadedState)` matches the exact saved set (not the seed-rolled default). Also test `PacketWriter`/`PacketReader` round-trip via `SerializableRun.Serialize`/`Deserialize`.

---

## Phase 5B — Interactive Pack Draft at Run Start (Native Neow Pre-Blessing Pages)
- **Hook:** One minimal Harmony **Prefix** on `Neow.GenerateInitialOptions()` (`[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]`).
- **Why Prefix (not Postfix):** Running a Prefix ensures vanilla `Neow.GenerateInitialOptions()` does not roll any Neow rewards/cards from the wrong packs before the player drafts, and avoids stashing `originalNeowOptions`. Once the final pack is picked and persisted via `PackRunState.SetActivePacksForRun(...)`, calling `GenerateInitialOptions()` passes straight through the prefix and displays fresh Neow blessings via `SetEventState(...)`.
- **When it activates:**
  - `Owner.Character is ThePackmaster`
  - `!PackmasterConfig.AllPacksMode`
  - `!PackRunState.HasPersistedActivePacks(Owner.RunState)` (so saving/reloading after drafting skips straight to Neow blessings; saving mid-draft replays the deterministic draft cleanly from page 1).
  - `availableNonAlwaysPacks.Count > slotsToFill` (there is actually a choice to make; otherwise persist all packs immediately and pass through to vanilla Neow).
- **Draft Algorithm (authentic Packmaster feel, scales cleanly from 8 to 70+ packs):**
  - Use a dedicated deterministic RNG (`new Rng((uint)StringHelper.GetDeterministicHashCode(runState.Rng.StringSeed + "_PACKMASTER_DRAFT"))`) so Neow's own blessing RNG is never mutated.
  - Always include `AlwaysInclude` packs (`CoreSet`).
  - `slotsToFill = Math.Min(PackmasterConfig.PacksPerRun, PackRegistry.AllPacks.Count) - alwaysInclude.Count`.
  - `choicePicks = Math.Min(3, slotsToFill)`, `randomPicks = slotsToFill - choicePicks`.
  - Roll `randomPicks` packs from `remainingPool` first.
  - For each of the `choicePicks` pages:
    - Pick up to 3 distinct candidate packs from `remainingPool` (keeping unpicked candidates in `remainingPool` when `remainingPool.Count < 3 * remainingPicks` so even 8–10 total packs never starve later pages!).
    - Present each candidate as a native `EventOption(neow, onChosen, title, description, optionKey, Array.Empty<IHoverTip>())` (same non-relic `EventOption` pattern Neow uses for `OnBlitzOptionSelected` / `GenerateNeowModifiers`).
    - On pick: add chosen pack to `chosen`, remove only the chosen pack from `remainingPool`, and transition via cached `EventModel.SetEventState(...)` to either the next pick page or (after the final pick) call `PackRunState.SetActivePacksForRun(runState, chosen)` and `SetEventState(neow.InitialDescription, neow.GenerateInitialOptions())`.
- **Autotest (`NeowInteractivePackDraft`):**
  - Test entering Neow on a new Packmaster run with 8+ packs and `PacksPerRun = 5`: verify Prefix intercepts and presents pack choice `EventOption`s, pick options through all draft pages, verify `PackRunState.HasPersistedActivePacks(runState)` is true with exact `CoreSet` + random + picked packs, and verify Neow's normal blessing options are now active!

---

## Phase 5C — Core Packmaster Relics (5 Iconic Relics)
Add 5 StS 1 Packmaster relics to `Packmaster/Relics/` (with StS 1 icons resized to `94x94`, `_outline.png`, and `big/256x256`, plus localization in `relics.json`):
1. **`CollectorBadge`** (`RelicRarity.Rare` — StS 2 has no Boss chest tier): Each turn, after you play a card from 3 different Packs, gain 1 Energy next turn (`EnergyNextTurnPower(1)`). Guards `cardPlay.Card.Owner == Owner`, tracks `HashSet<string>` of pack IDs played this turn, clears on `AfterSideTurnStart` / `AfterCombatEnd`, updates `ShowCounter` / `DisplayAmount` / `Status`.
2. **`GemOfUnity`** (`RelicRarity.Uncommon`): Once per combat, after you play a card from each active Pack in the current run, gain 20 Block (`ValueProp.Unpowered`), 1 Strength, 1 Dexterity, and heal 5 HP. `IsAllowedInternal(IRunState)` checks `!PackmasterConfig.AllPacksMode` only (never locks out when `PacksPerRun > AllPacks.Count`).
3. **`PMBoosterPack`** (`RelicRarity.Common`, `HasUponPickupEffect => true`): Upon pickup (`AfterObtained`), uses a shared ~15-line `PackRelicHelper` to pick 1 random Pack via `Owner.RunState.Rng`, instantiate its draftable cards via `Owner.RunState.CreateCard(canonical, Owner)`, open `CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), ...)` (choose 1), add to `PileType.Deck`, and preview via `CardCmd.PreviewCardPileAdd`.
4. **`PMCollection`** (`RelicRarity.Shop`, `HasUponPickupEffect => true`): Uses the same shared `PackRelicHelper` with `minSelect: 0, maxSelect: 10` so the player can add any subset of a random Pack's cards to their deck.
5. **`BagOfHolding`** (`RelicRarity.Rare`, `HasUponPickupEffect => true`): Starter-upgrade relic replacing `HandyHaversack` (`IsAllowedInternal` checks `runState.Players.Any(p => p.Relics.Any(r => r is HandyHaversack))`, `AfterObtained` removes `HandyHaversack` via `RelicCmd.Remove` if present). At start of combat (`BeforeCombatStart`), sets counter to `Owner.Deck.Cards.Count / 5`; each turn (`AfterEnergyReset` guarded by `player == Owner`), if counter > 0, grants +1 Energy and decrements counter.
- **Autotest (`CorePackmasterRelicsInCombat`):** Test all 5 relics end-to-end (including a deterministic test `ICardSelector` for `PMCollection` selecting 2 cards when `minSelect == 0`, `BagOfHolding` replacing `HandyHaversack` and expiring after `deck/5` turns, `CollectorBadge` triggering once per turn at 3 distinct packs and resetting next turn, and `GemOfUnity` triggering once per combat!).

---

## Phase 5D — Port 2 More Clean Packs (Total: 9 Packs)
Port 2 additional StS2-native packs (`ShamanPack` and `EnergyAndEchoPack`, or `WardenPack` using BaseLib's native `ScryVar`) so that even at default `PacksPerRun = 7`, every new Packmaster run has a full 3-choice x 3-page interactive Pack Draft at Neow!

---

## Open Questions Saved for User Review Later
1. Should `BagOfHolding` and `CollectorBadge` stay `RelicRarity.Rare` (so they appear in normal StS 2 chests/elites/shops) or be offered as an Ancient relic option (StS 2's replacement for Boss relics)?
2. For `Neow` Pack Draft, do we want CoreSet + 3 Random + 3 Choice (authentic StS 1 default) or all-choice while the total pack count is small (8–12 packs)?
3. When should we replace `PlaceholderCharacterModel` / add full Packmaster Spine combat visuals & layered energy orb?
