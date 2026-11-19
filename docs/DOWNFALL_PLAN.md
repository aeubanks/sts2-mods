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

### Phase 4: The Champ (Fourth Complete Playable Downfall Character)
Port **The Champ** (`CharKey = "champ"`, `80 HP`, `99 Gold`, authentic Spine stance poses from `champ.atlas`/`champ.json`, at most 1 tiny Harmony prefix on `VigorPower` for `CrookedStrike`) in 6 clean vertical slices (~15–20 cards/batch), each tested, High-model reviewed, and committed:

- **Core StS 2 Architecture (Simplicity-First):**
  - **Shared `DownfallEcho` promotion:** Promote `GremlinMob`'s tiny `ConditionalWeakTable<CardModel, object>` + `MakeEcho` helper to `Downfall/Core/DownfallEcho.cs` so Gremlins, Champ (`SpectersHand`, `TripleStrike`), and future Downfall characters share one Echo helper.
  - **Stances (`Downfall/Champ/ChampStances.cs` + 3 Stance Powers + 2 Ultimate Duration Powers):**
    - `ChampStanceKind`: `None`, `Berserker`, `Defensive`, `Ultimate`.
    - Active stance represented natively by one of 3 `ChampStancePower` subclasses (`PowerStackType.Single`, `DynamicVars["Charges"]` starting at `3m`, `DisplayAmount => DynamicVars["Charges"].IntValue`), swapping Champ's combat `Sprite2D.Texture` across all 4 authentic Spine poses (`Idle`, `IdleBerserker`, `IdleDefensive`, `IdleUltimate`):
      - `BerserkerStancePower`: **Skill Bonus** = Gain `2` (+`DoubleStylePower` +`DoubleStyleThisTurnPower`) `VigorPower` (native StS 2!). **Finisher Bonus** = Gain `1` `StrengthPower`.
      - `DefensiveStancePower`: **Skill Bonus** = Gain `3` (+`DoubleStylePower` +`DoubleStyleThisTurnPower`) `CounterPower`. **Finisher Bonus** = Gain `6` (+`DefensiveTrainingManual` relic) Block (`ValueProp.Unpowered`).
      - `UltimateStancePower`: BOTH Berserker AND Defensive Skill Bonus and Finisher Bonus!
    - **Skill Bonus charges (`BeforeCardPlayed`):** Playing a `CardType.Skill` while in a Stance with `Charges > 0` spends 1 charge (charges do **not** refresh per turn — only on entering a *different* Stance) and triggers the Stance's Skill Bonus (departing-stance timing matches StS 1's `stance.onPlayCard` so Opener Skills from Neutral don't burn a charge of the newly entered Stance, while same-stance Opener Skills spend 1 charge and do not reset charges). Entering the same Stance is a no-op.
    - **Ultimate duration & lock:** `UltimateFormPower` locks the player in `Ultimate` Stance (blocking stance exits/switches) and decrements at end of round, removing `UltimateFormPower` **before** exiting to Neutral on expiration. `UltimateFormNextTurnPower` enters `Ultimate` Stance and applies `UltimateFormPower` at the start of next turn.
    - **Technique:** Explicit `ChampStances.TriggerSkillBonusAsync` (triggers active Stance's Skill Bonus directly without consuming a charge).
    - **Finisher (`ChampCard.IsFinisher`):** Only playable when in a Stance (`IsPlayable`, unless exempted by `SignatureFinisher`). Triggers active Stance's Finisher Bonus (skipped if Neutral via `SignatureFinisher`), increments combat/turn finisher counters, and exits Stance (unless `noExit`, `UltimateFormPower`, or `SignatureFinisher`). All Finisher/Combo reactors (`DancingMasterPower`, `FocusedBerPower`, `ChampionCrownUpgraded`, `Devastate`, `GladiatorsBookOfMartialProwess`) use native `AfterCardPlayed` directly!
    - **Combo (`ChampCard.IsCombo`):** `ChampStances.IsInBerserker` (Berserker or Ultimate) and `ChampStances.IsInDefensive` (Defensive or Ultimate), glowing gold on `ShouldGlowGoldInternal`.
    - **Single listener interface:** Keep ONLY `IChampStanceChangedListener` (used by `ImprovisingPower`, `IronFortressPower`, `BlackKnightsHelmet`, `SpectersHand`).
  - **Counter (`CounterPower` + `Riposte` token):**
    - `CounterPower` (`PowerStackType.Counter`): on `AfterDamageReceived` (when attacked by another creature with `props.IsPoweredAttack()`, even if fully blocked), snapshots `Amount`, removes `CounterPower` first (multi-hit safe!), adds `1` (+`ParryPower` stacks) 0-cost Ethereal Exhaust `Riposte` token Attack (`CardTag.Strike`) to Hand with damage equal to the snapshotted Counter, and notifies `GladiatorFormPower`.
  - **Vigor-spend tracking & `CrookedStrike`:**
    - `GladiatorFormPower` (every 3 Vigor / 3 Counter spent -> next-turn Vigor / Counter via `NextTurnVigorPower` / `NextTurnCounterPower`) and `RageAmulet` (every 8 Vigor spent -> +1 Strength) snapshot `Owner.GetPowerAmount<VigorPower>()` in `BeforeCardPlayed` (excluding `CrookedStrike`).
    - `CrookedStrike` ("Does not consume Vigor"): check `VigorPower` in `sts2.dll`; if no native opt-out hook exists, use **1 tiny Harmony prefix** on `VigorPower` skipping consumption when `cardPlay.Card is CrookedStrike`.
  - **No card-modifier framework:** `EnchantCrown`/`EnchantShield`/`EnchantSword` directly mutate the chosen in-combat `CardModel` (`EnergyCost.SetThisCombat(0)`, `Block.BaseValue += 8`, `Damage.BaseValue += 8`); `TripleStrike` sets a single `bool TriggersSkillBonus` on its 2 `ChampStrike` Echoes.
  - **Skipped dead StS 1 files:** Commented-out cards (`CrownThrowBackup`, `FightOn`, `FinalRevenge`, `HeartStrike`), dead powers (`BoomerangPower`, `FalseCounterPower`, `FocusedDefPower`, `CalledShotPower`, `ResolvePower`, `GladiatorStylePower`, `IronFortressVigorPower`, `ShieldWallPower`, `DrawLessNextTurnPower`), and Custom-Mode-only `ModFinisher`.
  - **Class disambiguation (`ModelId` uniqueness):** `ChampStrike`, `ChampDefend`, `ChampCalledShot` (avoids Hermit `CalledShot`), `ChampHeadbutt`, `ChampPerfectedStrike`, `UltimateStanceCard` (avoids `UltimateStancePower`).

- **Phase 4A — Champ Character, Visuals, Stances, Counter & Starter Kit:**
  - Render all 4 authentic Champ Spine poses (`champ.png`, `champ_berserker.png`, `champ_defensive.png`, `champ_ultimate.png`) + scenes (`champ_visuals.tscn`, `champ_rest_site.tscn`, `champ_merchant.tscn`, `char_select_bg.tscn`) + UI/energy icons + pools (`ChampCardPool`, `ChampRelicPool`, `ChampPotionPool`).
  - `TheChamp` (80 HP, 99 Gold), starter deck (10 cards: 4× `ChampStrike`, 3× `ChampDefend`, `BerserkersShout`, `DefensiveShout`, `Execute`), tokens (`Riposte`, `StanceDanceCrown`, plus 2 stance-choice token cards `ChooseBerserkerStance`/`ChooseDefensiveStance`), starting relic `ChampionCrown`.
  - Core infrastructure: `Downfall/Core/DownfallEcho.cs`, `ChampStances.cs`, `BerserkerStancePower`, `DefensiveStancePower`, `UltimateStancePower`, `UltimateFormPower`, `UltimateFormNextTurnPower`, `CounterPower`, `Testing/ChampTests.cs` (`ChampVisualsRuntime`, `ChampStarterCombat`).
- **Phase 4B — Champ Common Cards (19 active cards):** `AdrenalArmor`, `BringItOn`, `Circumvent`, `CrownThrow`, `EnGarde`, `Encircle`, `Endure`, `FlashCut`, `FocusedDefense`, `Haymaker`, `ChampHeadbutt`, `ChampPerfectedStrike`, `PreciseThrust`, `RapidStrikes`, `RisingSlash`, `ShieldSigil`, `StanceDance`, `Taunt`, `TornadoPunch` + `EnGardePower`, `DoubleStyleThisTurnPower` + `ChampCommonCardsInCombat`.
- **Phase 4C-1 — Champ Uncommon Cards Batch 1 (16 cards):** `ArenaPreparation`, `Backstep`, `BerserkerStyle` (*Hone Blade* -> `HonePower`), `BladeFlurry`, `ChampCalledShot`, `CrookedStrike` (+ `VigorPower` Harmony prefix if needed), `DeathBlow`, `DefensiveStyle` (*Double Style* -> `DoubleStylePower`), `Duel`, `EnragedBash`, `FaceSlap`, `FalseCounter`, `FanOfKnives`, `FancyFootwork`, `FocusedBerserking` (*All Out* -> `FocusedBerPower`), `GladiatorStyle` + `ChampUncommonBatch1InCombat`.
- **Phase 4C-2 — Champ Uncommon Cards Batch 2 (15 cards):** `GutPunch`, `IronFortress`, `Lariat`, `Parry`, `PreemptiveStrike`, `RageSigil`, `RecklessLeap`, `SetATrap`, `Shatter`, `ShieldSlam`, `ShieldWall`, `SkillfulDodge`, `SwordSigil`, `ViciousMockery`, `WindUp` + powers (`IronFortressPower`, `ParryPower`, `NoBlockNextTurnPower`, `EntangleNextTurnPower`, `Metallicize`-like non-decaying Block power if needed) + `ChampUncommonBatch2InCombat`.
- **Phase 4D — Champ Rare Cards (20 active cards):** `CheapShot`, `DancingMaster`, `Devastate`, `EnchantCrown`, `EnchantShield`, `EnchantSword`, `GladiatorForm`, `HeavySlash`, `HoldFirm`, `IgnorePain`, `Improvising`, `LastStand`, `MurderStrike`, `ShieldThrow`, `SteelEdge`, `StrikeOfGenius`, `SwordThrow`, `TripleStrike`, `UltimateStanceCard`, `VampiricStrike` + powers (`DancingMasterPower`, `GladiatorFormPower`, `NextTurnVigorPower`, `NextTurnCounterPower`, `ImprovisingPower`, `LastStandPower`, `PushThroughPower`, `StrikeOfGeniusPower`, `StrikeOfGeniusUpgradedPower`, `EnchantedShieldPower`) + `ChampRareCardsInCombat`.
- **Phase 4E — Champ Relics (15 remaining) & Potions (4):** `Barbells`, `BerserkersGuideToSlaughter`, `BlackKnightsHelmet` (`RelicRarity.Event`), `ChampStancesModRelic` (`RelicRarity.Event`), `ChampionCrownUpgraded`, `DefensiveTrainingManual`, `DeflectingBracers` (native block-expiry hook), `DuelingGlove`, `FightingForDummies`, `GladiatorsBookOfMartialProwess`, `LiftRelic` (`RelicRarity.Event`), `PowerArmor` (+1 Max Energy, clamps `VigorPower`/`CounterPower` total stack to `10`, no Block conversion), `RageAmulet` (every 8 Vigor spent -> +1 Strength), `SignatureFinisher` (0 cost via `TryModifyEnergyCostInCombat` guarding `!CostsX`, playable without Stance, never exits Stance, `MoonTalisman` deck-index pattern), `SpectersHand` (`DownfallEcho` of 0-cost Strike/Defend on stance enter); Potions: `CounterstrikePotion`, `OpenerPotion`, `TechPotion`, `UltimateStancePotion` + `ChampRelicsAndPotionsInCombat`.

### Phase 5+: Remaining 6 Downfall Characters (Same ~15–20 Card Vertical-Slice Process)
Order from cleanest StS 2 mechanical fit to most custom subsystems:
1. **The Snecko** (Muddle + Overflow + Offclass)
2. **The Guardian** (Defensive Mode + Stasis + Brace/Gems)
3. **The Hexaghost** (Soulburn + Afterlife + 6 Ghostflames)
4. **The Slime Boss** (Slimed + Lick + Tackle + Split Slimes)
5. **The Automaton** (Encode + Compile Functions)
6. **The Collector** (Doom + Collected Cards + Pyre/Torchhead)

---

## Open Questions Saved for User (Proceeding With Sensible Defaults)
1. **Gremlins HP model:** Defaulting to single `75 HP` + stance-like `GremlinMobPower` for StS 2 simplicity (zero run-save/death-intercept complexity). Can upgrade to faithful 5×16 persistent HP later if desired.
2. **Downfall boss-run mode:** Focusing on playable characters, cards, relics, potions, and powers in standard StS 2 runs first; reverse-act campaign / hero bosses deferred.
