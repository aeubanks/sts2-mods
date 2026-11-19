# Downfall StS2 — Porting Plan

## Reference Repository
- **Source:** `https://github.com/mikemayhemdev/DownfallSTS` (cloned locally at `/home/itg/repos/DownfallSTS`).
- **Contains all 10 Downfall playable characters:** Hermit (`hermit`), Gremlins (`gremlin`), Awakened One (`awakenedOne`), Slime Boss (`slimebound`), Guardian (`guardian`), Hexaghost (`theHexaghost`), Champ (`champ`), Automaton (`automaton`), Collector (`collector`), and Snecko (`sneckomod`).

## Guiding Principles
1. **Simplicity first:** Separate sibling StS 2 mod project at `/home/itg/src/sts2-mods/Downfall/` (deployed to `mods/Downfall/`), reusing our proven BaseLib v3.4.8 + Godot 4.5.1 Mono patterns from `Packmaster/`. Do not call `Harmony.PatchAll` until a Harmony patch is genuinely needed.
2. **Multi-character safety (BaseLib / StS 2 rules):**
   - **No `[Pool(...)]` on shared base classes:** `DownfallCard`, `DownfallRelic`, and `DownfallPotion` carry **no** `[Pool(...)]` attribute. Each character defines its own 3 pools (`HermitCardPool` with unique `Title`, `ShaderColor`, `DeckEntryCardColor`, energy icon paths; `HermitRelicPool`; `HermitPotionPool`) and thin character base classes (`HermitCard` with `[Pool(typeof(HermitCardPool))]`, etc.).
   - **Per-character asset scoping:** Store assets under `res://Downfall/images/{charKey}/{cards,relics,powers,potions,char,charui}/` and `res://Downfall/scenes/{charKey}/` so filenames (`strike.png`, `defend.png`, etc.) never collide across 10 characters.
   - **Flat localization tables:** BaseLib loads flat `res://Downfall/localization/eng/{characters,cards,relics,powers,potions,gameplay_ui}.json`. Every character supplies all required `characters.json` entries.
   - **Global `ModelId` uniqueness:** BaseLib derives `ModelId.Entry` (`DOWNFALL-<UPPER_SNAKE_TYPE_NAME>`) from the unqualified C# class name (ignoring namespace). Every `ModelDb` type in `Downfall.dll` must have a globally unique C# class name (prefix ambiguous names with the character, e.g. `HermitStrike`, `GremlinWardPower`, `AwakenedDrainPower`), asserted by `ModelDbAssetsAndLocIntegrity`.
   - **Token curses stay in character pools:** Character-specific token curses such as `MementoCard` (`CardType.Curse`) use `CardRarity.Token` inside their character's `CardPool` (never `CurseCardPool`), so vanilla random-curse effects never grant them to other characters.
3. **Real character visuals from day 1:** Render each character's authentic StS 1 Spine setup pose (or sprite) into crisp PNGs + minimal BaseLib-converted `.tscn` scenes (`CustomVisualPath` with `%Bounds` `mouse_filter = 2`, `CustomRestSiteAnimPath`, `CustomMerchantAnimPath`, `CustomCharacterSelectBg`, icons) — never ship Ironclad placeholders.
4. **Small, tested vertical slices:** Cap card batches at **~15–20 cards per sub-phase**. Every milestone compiles with 0 warnings/errors, packs cleanly, passes isolated headless autotests (`./scripts/test-downfall.sh`), gets a High-model `reviewer` simplicity/correctness review, and commits cleanly.
5. **Coexistence & test isolation with Packmaster:**
   - Independent mod ID (`Downfall`), asset root (`res://Downfall/...`), loc prefix (`DOWNFALL-...`), and autotest trigger (`DOWNFALL_AUTOTEST=1`).
   - `scripts/test-downfall.sh` sets `PACKMASTER_AUTOTEST=0 DOWNFALL_AUTOTEST=1`, supports `DOWNFALL_AUTOTEST_FILTER`, includes `Packmaster` in `mod_list` only when `"$STS2_DIR/mods/Packmaster"` exists on disk, and writes the isolated `user://default/1/settings.save` (never concurrent with `scripts/test.sh` and never touching Steam saves).
   - Keep `Testing/DownfallAutoTestRunner.cs` a thin dispatcher + generic `StartCombatAsync<TCharacter>` helper, splitting test suites per character (`Testing/HermitTests.cs`, `Testing/AwakenedOneTests.cs`, `Testing/GremlinsTests.cs`, etc.).

---

## Phased Milestones

### Phase 0: Downfall Mod Scaffold, Headless Smoke Harness & Spine Helper
- Create sibling mod project `Downfall/`:
  - `Downfall.csproj`, `Downfall.json`, `Directory.Build.props`
  - `project.godot` (`config/name="Downfall"`, `assembly_name="Downfall"`, `icon="res://Downfall/mod_image.png"`)
  - `export_presets.cfg` (`exclude_filter="Downfall.json"`)
  - `MainFile.cs` (no `Harmony.PatchAll` yet; boots `DownfallAutoTestRunner` when `DOWNFALL_AUTOTEST=1`)
  - `Downfall/mod_image.png`
- Create `scripts/test-downfall.sh` (`PACKMASTER_AUTOTEST=0 DOWNFALL_AUTOTEST=1`, `DOWNFALL_AUTOTEST_FILTER`, conditional `Packmaster` coexistence, timeout bumpable as characters grow).
- Create `Downfall/Testing/AutoTestAssert.cs` + `Downfall/Testing/DownfallAutoTestRunner.cs` running a **boot & Packmaster-coexistence smoke test** (0 unhandled errors, clean exit 0).
- Create reusable `scripts/render_spine_pose.py` to render StS 1 Spine `.atlas`/`.json`/`.png` setup poses into crisp 2×-supersampled StS 2 character sprites.

### Phase 1: The Hermit (First Complete Playable Downfall Character)
Port **The Hermit** in focused ~15–20 card commits (each reviewed + tested):
- **Phase 1A — Shared Base Classes, Hermit Visuals, Pools & Starter Kit:**
  - Introduce shared `DownfallCard`, `DownfallRelic`, `DownfallPower`, `DownfallPotion` (no `[Pool(...)]` on shared bases) and Hermit pools (`HermitCardPool`, `HermitRelicPool`, `HermitPotionPool`) + `HermitCard` / `HermitRelic` / `HermitPotion`.
  - `TheHermit` (`PlaceholderCharacterModel` with authentic Hermit combat/rest-site/merchant visuals from `Hermit.atlas`, `mouse_filter = 2` on `%Bounds`, char select BG/icon, map marker, outline, 75 HP, 99 Gold).
  - Starter deck (10 cards): 4× `HermitStrike`, 4× `HermitDefend`, 1× `Snapshot`, 1× `Covet`.
  - Starting relic: `OldLocket` (`Memento`) + token Curse `MementoCard` (`CardRarity.Token` in `HermitCardPool`, `Retain`, Apply 1 Vulnerable to EVERYONE).
  - Shared `HermitHelper` (`IsDeadOn`, `ConcentratedPower`, `BruisePower`, `RuggedPower`).
  - `ModelDbAssetsAndLocIntegrity` (validating all registered Downfall characters, pools, unique `ModelId`s, assets, textures, SmartFormat loc) + `Testing/HermitTests.cs` (live RestSite/Shop/Combat/Death visual runtime test + starter combat).
- **Phase 1B — Hermit Common Cards (20 cards)** + `HermitTests.cs` combat coverage.
- **Phase 1C-1 — Hermit Uncommon Cards Batch 1 (~17 cards)** + combat tests.
- **Phase 1C-2 — Hermit Uncommon Cards Batch 2 (~17 cards)** + combat tests.
- **Phase 1D — Hermit Rare Cards (17 cards) + `ImpendingDoom`** + combat tests.
- **Phase 1E — Hermit Relics (13 remaining) & Potions (3: `Tonic`, `BlackBile`, `Eclipse`)** + tests.

### Phase 2: The Awakened One (Second Complete Playable Downfall Character)
- **Phase 2A — Awakened One Character, Visuals, Pools & Starter Kit:**
  - `TheAwakenedOne` (70 HP, 99 Gold, authentic `awakened.atlas` visuals, pools, char select BG/icons).
  - Starter deck: 4× `AwakenedStrike`, 4× `AwakenedDefend`, `TalonRake`, `Hymn` (+ `Ceremony` token).
  - Starting relic: `RippedDoll` (Conjure on turns 1–2).
  - Shared `AwakenedHelper` (Chant check, Conjure spell pool, Awakening threshold) + `Testing/AwakenedOneTests.cs`.
- **Phase 2B–2F — Awakened One Cards (~15–20/batch), Spells, Relics & Potions**, each reviewed + tested.

### Phase 3: The Gremlins (Third Complete Playable Downfall Character)
- **Phase 3A — Gremlins Character, Visuals, Pools & Starter Kit:**
  - `TheGremlins` (authentic Gremlin Gang visuals from `angry`/`sneak`/`fat`/`shield`/`wizard` Spine assets, pools, char select BG/icons).
  - **HP & Mob Model (Simplicity-First StS 2 Fit):** Use a single `75 HP` pool + stance-like `GremlinMobPower` tracking the active gremlin (`Mad`, `Sneaky`, `Fat`, `Shield`, `Wizard`) and applying its passive bonus (`Mad`: +2 Strength when taking unblocked hit; `Sneaky`: +2 damage on Attacks; `Fat`: Weak on Attacks; `Shield`: +2 Block on Skills; `Wizard`: Wiz charges), with `Swap` switching the active gremlin cleanly via `CardSelectCmd`/next-in-rotation — **zero run-save HP persistence or fatal-damage interception hooks**. (A faithful 5×16 persistent-HP mode can be added later if requested.)
  - Starter deck: 4× `GremlinStrike`, 4× `GremlinDefend`, `TagTeam`, `GremlinDance`.
  - Starting relic: `GremlinKnob` ("Mob Leader's Staff") + `Testing/GremlinsTests.cs`.
- **Phase 3B–3F — Gremlins Cards (~15–20/batch), Relics & Potions**, each reviewed + tested.

### Phase 4+: Remaining 7 Downfall Characters (Same ~15–20 Card Vertical-Slice Process)
Order from cleanest StS 2 mechanical fit to most custom subsystems:
1. **The Champ** (Stances + Finisher + Counter)
2. **The Snecko** (Muddle + Overflow + Offclass)
3. **The Guardian** (Defensive Mode + Stasis + Brace/Gems)
4. **The Hexaghost** (Soulburn + Afterlife + 6 Ghostflames)
5. **The Slime Boss** (Slimed + Lick + Tackle + Split Slimes)
6. **The Automaton** (Encode + Compile Functions)
7. **The Collector** (Doom + Collected Cards + Pyre/Torchhead)

---

## Open Questions Saved for User (Proceeding With Sensible Defaults)
1. **Gremlins HP model:** Defaulting to single `75 HP` + stance-like `GremlinMobPower` for StS 2 simplicity (zero run-save/death-intercept complexity). Can upgrade to faithful 5×16 persistent HP later if desired.
2. **Downfall boss-run mode:** Focusing on playable characters, cards, relics, potions, and powers in standard StS 2 runs first; reverse-act campaign / hero bosses deferred.
