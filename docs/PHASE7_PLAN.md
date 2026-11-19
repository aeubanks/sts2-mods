# Phase 7 Plan — HermitPack & DistortionPack (12th & 13th Packs / 130 Draftable Cards)

## Goals
1. Port 2 more iconic, high-synergy, zero-Harmony-patch packs:
   - **Phase 7A (`HermitPack` — 12th total pack / 120 draftable cards):** Downfall guest pack featuring Dead On positioning (`Headshot`), debuff-to-Strength conversion (`Determination`, `HoleUp`, `Virtue`, `Quickdraw`), and `DeadOrAlive` Bounty!
   - **Phase 7B (`DistortionPack` — 13th total pack / 130 draftable cards):** Exhaust-triggered HP-loss (`DistortionPower`), `Rue`, `Deterioration`, and HP-loss multi-hit (`Shatter`) — all using native StS 2 hooks (`AfterCardExhausted`, `DamageReceivedEntry`, `ModifyCardPlayResultLocation`) with **zero Harmony patches** (unlike StS 1 which required 2 patches!).
2. Keep complexity strictly minimal and validate every card, power, and edge case via headless autotests.

---

## Phase 7A: `HermitPack` (10 Draftable Cards + 3 Powers)

### Powers (`Packmaster/Powers/`)
1. **`DeterminationPower`** (`Buff`, `Counter`):
   - Overrides `AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)`.
   - Triggers when `power.Owner == Owner && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding && amount != 0m && power.GetTypeForAmount(amount) == PowerType.Debuff` (verified in `sts2.dll`: `GetTypeForAmount` returns `PowerType.Buff` for negative `amount` on non-`AllowNegative` debuffs like Weak decay / `Virtue`, and `PowerType.Debuff` for negative `amount` on `AllowNegative` stats like Strength/Dexterity!).
   - Flashes and applies `StrengthPower(Amount)` to `Owner`.
2. **`OverwhelmingPowerPower`** (`Debuff`, `Counter`):
   - Overrides `BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)`: if `participants.Contains(Owner) && Owner.Player?.PlayerCombatState?.Energy == 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding`, flashes and deals `Amount` `ValueProp.Unblockable | ValueProp.Unpowered` HP loss to `Owner`.
3. **`DrawLessNextTurnPower`** (`Debuff`, `Counter`):
   - Mirrors StS 2's `DrawCardsNextTurnPower`: `ModifyHandDraw(Player, decimal)` returns `Math.Max(0m, count - Amount)` when `player == Owner.Player && AmountOnTurnStart > 0`; `AfterModifyingHandDraw` calls `await PowerCmd.Remove(this)` (guarded by `AmountOnTurnStart > 0`).
   - Being a `PowerType.Debuff` ensures authentic StS 1 synergy with `DeterminationPower` and `Virtue`!

### Cards (`Packmaster/Cards/Hermit/`)
Exact StS 1 values/upgrades verified from `/home/itg/repos/PackmasterCharacter/src/main/java/thePackmaster/cards/hermitpack/`:
1. **`Quickdraw`** (1 cost Attack, Common, AnyEnemy): deal `9 (+2)` dmg, draw `2 (+1)` cards, apply `DrawLessNextTurnPower(1)` to self.
2. **`Glare`** (0 cost Skill, Common, AnyEnemy): apply `1` Weak and `1` Vulnerable; upgrade adds `Retain`.
3. **`HoleUp`** (1 cost Skill, Common, Self, `GainsBlock => true`): gain `12 (+4)` Block, apply `2` Weak to self.
4. **`Headshot`** (1 cost Attack, Common, AnyEnemy): deal `7 (+2)` dmg; deals **double** damage if played from the middle of your Hand (`Math.Abs(2 * idx + 1 - hand.Count) <= 1`).
   - Verified in `sts2.dll`: `CardModel.OnPlayWrapper` moves the card from `Hand` to `Play` *before* `BeforeCardPlayed`, whereas `PlayCardAction.ExecuteAction` calls `CanPlay()` -> `Hook.ShouldPlay()` right before moving the card out of `Hand`. Capture `_deadOnWhenPlayed = Pile?.Type == PileType.Hand && IsInMiddleOfHand()` in `ShouldPlay(CardModel card, AutoPlayType autoPlayType)` when `card == this`, use live `IsInMiddleOfHand()` when `Pile?.Type == PileType.Hand` and locked `_deadOnWhenPlayed` when `Pile?.Type == PileType.Play` inside `ModifyDamageMultiplicative`, and clear in `AfterCardPlayed` on the final play in series (`PlayIndex == PlayCount - 1`). Glows gold when Dead On.
5. **`Determination`** (1 cost Power, Uncommon, Self): apply `DeterminationPower(1)`; upgrade adds `Innate`.
6. **`Virtue`** (0 cost Skill, Uncommon, Self, `Retain`): snapshot `Owner.Creature.Powers.Where(p => p.TypeForCurrentAmount == PowerType.Debuff).ToList()`, and for each debuff still on `Owner`, call `PowerCmd.Remove(p)` if `p.StackType == PowerStackType.Single || Math.Abs(p.Amount) <= amt`, else `PowerCmd.ModifyAmount(choiceContext, p, p.Amount > 0 ? -amt : amt, Owner.Creature, this)`.
7. **`FlashPowder`** (1 cost Skill, Uncommon, AllEnemies, `Exhaust`, `GainsBlock => true`): gain `5` Block, ALL enemies permanently lose `1 (+1)` Strength (`PowerCmd.Apply<StrengthPower>(..., -amount, ...)`).
8. **`Purgatory`** (3 cost Attack, Rare, AllEnemies, `Ethereal`): deal `24 (+6)` damage to ALL enemies.
9. **`OverwhelmingPower`** (1 cost Power, Rare, Self): gain `3` Energy, draw `3` cards, apply `OverwhelmingPowerPower(4 (-2 -> 2))` to self.
10. **`DeadOrAlive`** (X cost Attack, `HasEnergyCostX => true`, Rare, AnyEnemy, `Exhaust`, `CanBeGeneratedInCombat => false`): deal `8 (+3)` damage `x = ResolveEnergyXValue()` times (`if (x <= 0) return;`); if Fatal (`shouldTriggerFatal` from `target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal())` AND `attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled)`), grant Bounty Gold (`15` Normal / `40` Elite / `100` Boss via `CombatState.Encounter?.RoomType` and `PlayerCmd.GainGold`).

---

## Phase 7B: `DistortionPack` (10 Draftable Cards + 3 Powers)

### Powers (`Packmaster/Powers/`)
1. **`DistortionPower`** (`Debuff`, `Counter`):
   - Overrides `AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)` (including Ethereal exhausts like `Dazed`!).
   - When `Owner.IsAlive && Amount > 0 && !CombatManager.Instance.IsOverOrEnding`: captures `int dmg = Amount;`, flashes, decrements `this` via `PowerCmd.Decrement(this)` before damage (re-entrancy safe), and deals `dmg` `ValueProp.Unblockable | ValueProp.Unpowered` HP loss to `Owner`.
2. **`RuePower`** (`Debuff`, `Counter`):
   - Arms eligible plays via `private readonly HashSet<CardPlay> _armedPlays = [];` in `BeforeCardPlayed` (`cardPlay.Card.Owner == Owner.Player && cardPlay.PlayIndex == 0 && _armedPlays.Count < Amount`), redirects non-Power `PileType.Discard` -> `PileType.Exhaust` in `ModifyCardPlayResultLocation`, and consumes + decrements in `AfterCardPlayed` (never self-decrements on `Rue`'s own play!).
3. **`DeteriorationPower`** (`Buff`, `Counter`):
   - Overrides `BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)`: if `participants.Contains(Owner)` and `DiscardPile` is non-empty, picks 1 random card from `DiscardPile`, flashes, exhausts it via `CardCmd.Exhaust`, and (if still alive & combat not ending) gains `Amount` `ValueProp.Unpowered` Block.

### Cards (`Packmaster/Cards/Distortion/`)
Exact StS 1 stats/rarities/tags verified from `/home/itg/repos/PackmasterCharacter/src/main/java/thePackmaster/cards/distortionpack/`:
1. **`S_r_ke`** (Common, 1 cost Attack, `CardTag.Strike`): `6 (+1)` dmg + `4 (+1)` Distortion.
2. **`Deconstruct`** (Common, 1 cost Skill, Exhaust): `5 (+1)` Distortion to ALL enemies.
3. **`Darken`** (Uncommon, 2 cost Skill): `7 (+2)` Distortion; `+4 (+2)` more in a single `PowerCmd.Apply` call if target has any `p.TypeForCurrentAmount == PowerType.Debuff && p is not DistortionPower`.
4. **`BurningAct`** (Uncommon, 1 cost Skill): draw `4`, exhaust `2` of the drawn cards still in Hand (random unupgraded; player choice upgraded; if `<= 2` remain, exhaust all directly).
5. **`MindMaze`** (Uncommon, X cost Skill, `GainsBlock => true`): gain `11 (+4)` Block X times, put X `Dazed` on top of Draw Pile.
6. **`Deterioration`** (Uncommon, 1 cost Power): `DeteriorationPower(5 (+2))`.
7. **`Shatter`** (Rare, 0 cost Attack): `4 (+2)` dmg once per time target lost HP this turn (`DamageReceivedEntry` with `UnblockedDamage > 0`).
8. **`Rue`** (Rare, 0 cost Skill): gain `2` Energy, draw `1 (+1)`, apply `RuePower(3)`.
9. **`Static`** (Rare, 1 cost Attack): single multi-hit `DamageCmd` (`1` dmg `4 (+1)` times to random enemy) + per-hit `DistortionPower(result.UnblockedDamage)` for each living receiver with `UnblockedDamage > 0`.
10. **`CursedCircle`** (Uncommon, 0 cost Skill, Exhaust removed on upgrade): you and ALL enemies lose Strength this turn (`PiercingWailPower`) equal to cards in Hand (excluding self).

---

## Execution & Quality Gates
- Implement **7A (`HermitPack`) -> review -> test -> commit**, then **7B (`DistortionPack`) -> review -> test -> commit**.
- Every commit requires High-model `reviewer` subagent review (`Model: "inherit"`) + `./scripts/test.sh --pack` passing with 0 warnings, 0 errors, 100% pass rate.
