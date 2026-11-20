using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Orbs;

public abstract class GhostflameOrb : DownfallOrb
{
    private bool _isIgnited;
    private int _triggerProgress;

    protected override string CharacterKey => TheHexaghost.CharKey;

    public override Color DarkenedColor => new(0.45f, 0.45f, 0.50f, 0.85f);
    public override bool ShowSecondaryLabel => true;

    public bool IsIgnited => _isIgnited;
    public int TriggerProgress => _triggerProgress;
    public abstract int TriggersRequired { get; }
    protected abstract string TriggerUnitSuffix { get; }

    protected Player? SafeOwner => IsMutable ? Owner : null;

    public bool IsActiveGhostflame =>
        SafeOwner is not null && HexaMechanics.GetActiveGhostflame(SafeOwner) == this;

    protected bool IsCoordinatorOrb =>
        SafeOwner is not null && HexaMechanics.GetGhostflames(SafeOwner).FirstOrDefault() == this;

    public override string SpriteTexturePath =>
        _isIgnited
            ? $"res://Downfall/images/{CharacterKey}/orbs/{IconKey}_bright.png"
            : CustomIconPath;

    public override Color SpriteTint
    {
        get
        {
            if (IsActiveGhostflame)
                return Colors.White;
            return _isIgnited
                ? new Color(0.88f, 1.0f, 0.88f, 1.0f)
                : new Color(0.58f, 0.58f, 0.65f, 0.85f);
        }
    }

    public override Vector2 SpriteScale =>
        IsActiveGhostflame ? new Vector2(1.18f, 1.18f) : Vector2.One;

    public override string SecondaryLabelText =>
        _isIgnited
            ? "ON"
            : $"{Math.Max(0, TriggersRequired - _triggerProgress)}{TriggerUnitSuffix}";

    public override decimal EvokeVal => Math.Max(0, TriggersRequired - _triggerProgress);

    internal void MarkIgnited()
    {
        AssertMutable();
        _isIgnited = true;
        _triggerProgress = 0;
    }

    internal void MarkExtinguished()
    {
        AssertMutable();
        _isIgnited = false;
        _triggerProgress = 0;
    }

    protected void SetTriggerProgress(int value)
    {
        AssertMutable();
        _triggerProgress = Math.Max(0, value);
    }

    public abstract Task OnIgniteAsync(PlayerChoiceContext choiceContext, Player player);

    public abstract Task OnCardPlayedTriggerAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay);

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (IsCoordinatorOrb && side == CombatSide.Player && participants.Contains(Owner.Creature))
        {
            HexaCombatState.Get(combatState).OnPlayerTurnStarted();
            HexaMechanics.RefreshGhostflameVisuals(Owner);
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!IsCoordinatorOrb
            || cardPlay.Card.Owner != Owner
            || Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var state = HexaCombatState.Get(combatState);
        var active = HexaMechanics.GetActiveGhostflame(Owner);
        state.WasActiveIgnitedBeforeCardPlay = active?.IsIgnited ?? false;

        if (active is not null && !active.IsIgnited)
        {
            await active.OnCardPlayedTriggerAsync(new BlockingPlayerChoiceContext(), cardPlay);
            HexaMechanics.RefreshGhostflameVisuals(Owner);
        }
    }

    public override Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (IsCoordinatorOrb
            && card.Owner == Owner
            && Owner.Creature.CombatState is { } combatState)
        {
            var state = HexaCombatState.Get(combatState);
            state.CardsExhaustedThisTurn++;
            if (causedByEthereal)
                state.EtherealExhaustsThisTurn++;
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!IsCoordinatorOrb
            || side != CombatSide.Player
            || !participants.Contains(Owner.Creature)
            || Owner.Creature.CombatState is null
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var active = HexaMechanics.GetActiveGhostflame(Owner);
        if (active is not null)
        {
            if (HexaMechanics.PreventsEndTurnAdvance(Owner))
            {
                await HexaMechanics.ExtinguishAsync(choiceContext, Owner, active);
            }
            else if (active.IsIgnited)
            {
                await HexaMechanics.AdvanceAsync(choiceContext, Owner, isEndTurn: true);
            }
        }

        // Exact DownfallSTS rule: Slot 5 auto-extinguishes at end of turn only when it is an Inferno Ghostflame.
        var flames = HexaMechanics.GetGhostflames(Owner);
        if (flames.Count > 5 && flames[5] is InfernoGhostflameOrb inferno && inferno.IsIgnited)
        {
            await HexaMechanics.ExtinguishAsync(choiceContext, Owner, inferno);
        }

        HexaMechanics.RefreshGhostflameVisuals(Owner);
    }
}

/// <summary>
/// Searing Ghostflame: Ignites when 2 Attacks are played.
/// Ignition: Apply (3 + Intensity) Soulburn to a random enemy twice.
/// </summary>
public sealed class SearingGhostflameOrb : GhostflameOrb
{
    public const int BaseSoulburn = 3;

    public override int TriggersRequired => 2;
    protected override string TriggerUnitSuffix => "A";

    private int RawSoulburn => BaseSoulburn + HexaMechanics.GetIntensity(SafeOwner);
    private int DisplaySoulburn => Math.Max(0, RawSoulburn + HexaMechanics.GetSoulburnBonus(SafeOwner));

    public override decimal PassiveVal => DisplaySoulburn;
    public override string PrimaryLabelText => $"{DisplaySoulburn}x2";

    public override async Task OnCardPlayedTriggerAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsIgnited || cardPlay.Card.Type != CardType.Attack)
            return;

        SetTriggerProgress(TriggerProgress + 1);
        if (TriggerProgress >= TriggersRequired)
            await HexaMechanics.IgniteAsync(choiceContext, Owner, this);
    }

    public override async Task OnIgniteAsync(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return;

        int repeat = HexaMechanics.GetFlameRepeatCount(player, out bool affectAll);
        for (int hit = 0; hit < 2; hit++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                return;

            int rawAmount = BaseSoulburn + HexaMechanics.GetIntensity(player);
            if (affectAll)
            {
                for (int r = 0; r < repeat; r++)
                {
                    foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
                    {
                        if (CombatManager.Instance.IsOverOrEnding)
                            return;
                        await HexaMechanics.ApplySoulburnAsync(choiceContext, enemy, rawAmount, player.Creature);
                    }
                }
            }
            else
            {
                var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
                var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
                if (target is not null)
                    await HexaMechanics.ApplySoulburnAsync(choiceContext, target, rawAmount, player.Creature);
            }
        }
    }
}

/// <summary>
/// Crushing Ghostflame: Ignites when 2 Skills are played.
/// Ignition: Deal (3 + Intensity) damage to a random enemy twice.
/// </summary>
public sealed class CrushingGhostflameOrb : GhostflameOrb
{
    public const int BaseDamage = 3;

    public override int TriggersRequired => 2;
    protected override string TriggerUnitSuffix => "S";

    private int EffectiveDamage => Math.Max(0, BaseDamage + HexaMechanics.GetIntensity(SafeOwner));

    public override decimal PassiveVal => EffectiveDamage;
    public override string PrimaryLabelText => $"{EffectiveDamage}x2";

    public override async Task OnCardPlayedTriggerAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsIgnited || cardPlay.Card.Type != CardType.Skill)
            return;

        SetTriggerProgress(TriggerProgress + 1);
        if (TriggerProgress >= TriggersRequired)
            await HexaMechanics.IgniteAsync(choiceContext, Owner, this);
    }

    public override async Task OnIgniteAsync(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return;

        int repeat = HexaMechanics.GetFlameRepeatCount(player, out bool affectAll);
        for (int hit = 0; hit < 2; hit++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                return;

            int dmg = Math.Max(0, BaseDamage + HexaMechanics.GetIntensity(player));
            if (dmg <= 0)
                continue;

            if (affectAll)
            {
                for (int r = 0; r < repeat; r++)
                {
                    var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
                    if (enemies.Count == 0 || CombatManager.Instance.IsOverOrEnding)
                        return;
                    await CreatureCmd.Damage(choiceContext, enemies, (decimal)dmg, ValueProp.Unpowered, player.Creature);
                }
            }
            else
            {
                var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
                var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
                if (target is not null)
                    await CreatureCmd.Damage(choiceContext, target, dmg, ValueProp.Unpowered, player.Creature);
            }
        }
    }
}

/// <summary>
/// Bolstering Ghostflame: Ignites when a Power is played.
/// Ignition: Gain (4 + Intensity) Block and 1 Strength.
/// </summary>
public sealed class BolsteringGhostflameOrb : GhostflameOrb
{
    public const int BaseBlock = 4;
    public const int BaseStrength = 1;

    public override int TriggersRequired => 1;
    protected override string TriggerUnitSuffix => "P";

    private int EffectiveBlock => Math.Max(0, BaseBlock + HexaMechanics.GetIntensity(SafeOwner));

    public override decimal PassiveVal => EffectiveBlock;
    public override string PrimaryLabelText => EffectiveBlock.ToString();

    public override async Task OnCardPlayedTriggerAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsIgnited || cardPlay.Card.Type != CardType.Power)
            return;

        SetTriggerProgress(TriggerProgress + 1);
        if (TriggerProgress >= TriggersRequired)
            await HexaMechanics.IgniteAsync(choiceContext, Owner, this);
    }

    public override async Task OnIgniteAsync(PlayerChoiceContext choiceContext, Player player)
    {
        int repeat = HexaMechanics.GetFlameRepeatCount(player, out _);
        for (int r = 0; r < repeat; r++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            int block = Math.Max(0, BaseBlock + HexaMechanics.GetIntensity(player));
            await PowerCmd.Apply<StrengthPower>(choiceContext, player.Creature, BaseStrength, player.Creature, null);
            if (block > 0 && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CreatureCmd.GainBlock(player.Creature, block, ValueProp.Unpowered, null);
        }
    }
}

/// <summary>
/// Inferno Ghostflame: Ignites when 3 Energy is spent. Extinguishes at end of turn.
/// Ignition: Deal (4 + Intensity) damage to a random enemy for each Ignited Ghostflame,
/// then Extinguish all other Ignited Ghostflames. If all 6 were Ignited, gain 2 Intensity.
/// </summary>
public sealed class InfernoGhostflameOrb : GhostflameOrb
{
    public const int BaseDamage = 4;

    public override int TriggersRequired => 3;
    protected override string TriggerUnitSuffix => "E";

    private int EffectiveDamage => Math.Max(0, BaseDamage + HexaMechanics.GetIntensity(SafeOwner));

    public override decimal PassiveVal => EffectiveDamage;

    public override string PrimaryLabelText
    {
        get
        {
            int ignited = HexaMechanics.GetIgnitedCount(SafeOwner);
            int hits = IsIgnited ? ignited : ignited + 1;
            return $"{EffectiveDamage}x{hits}";
        }
    }

    public override async Task OnCardPlayedTriggerAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsIgnited)
            return;

        int spent = cardPlay.Resources.EnergySpent;
        if (spent <= 0)
            return;

        SetTriggerProgress(TriggerProgress + spent);
        if (TriggerProgress >= TriggersRequired)
            await HexaMechanics.IgniteAsync(choiceContext, Owner, this);
    }

    public override async Task OnIgniteAsync(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return;

        int dmg = Math.Max(0, BaseDamage + HexaMechanics.GetIntensity(player));
        int repeat = HexaMechanics.GetFlameRepeatCount(player, out bool affectAll);

        var ignitedFlames = HexaMechanics.GetGhostflames(player).Where(g => g.IsIgnited).ToList();
        int ignitedCount = ignitedFlames.Count;

        for (int i = ignitedFlames.Count - 1; i >= 0; i--)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                return;

            var gf = ignitedFlames[i];
            if (dmg > 0)
            {
                if (affectAll)
                {
                    for (int r = 0; r < repeat; r++)
                    {
                        if (CombatManager.Instance.IsOverOrEnding)
                            return;
                        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
                        if (enemies.Count == 0)
                            break;
                        await CreatureCmd.Damage(choiceContext, enemies, (decimal)dmg, ValueProp.Unpowered, player.Creature);
                    }
                }
                else
                {
                    var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
                    var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
                    if (target is not null)
                        await CreatureCmd.Damage(choiceContext, target, dmg, ValueProp.Unpowered, player.Creature);
                }
            }

            if (gf != this && !CombatManager.Instance.IsOverOrEnding)
                await HexaMechanics.ExtinguishAsync(choiceContext, player, gf);
        }

        if (ignitedCount >= HexaMechanics.WheelSlotCount && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<Powers.EnhancePower>(choiceContext, player.Creature, 2m, player.Creature, null);
        }
    }
}
