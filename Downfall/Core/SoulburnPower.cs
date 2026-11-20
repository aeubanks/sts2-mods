using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Hooks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Core;

/// <summary>
/// Implemented by powers on a Soulburned creature (e.g. Living Bomb) that make Soulburn detonation hit ALL enemies.
/// </summary>
public interface ISoulburnAoeModifier
{
    Task OnSoulburnAoeDetonatedAsync(PlayerChoiceContext choiceContext, Creature primaryTarget);
}

/// <summary>
/// Implemented by powers on the player/applier (e.g. Extra Crispy) that react whenever Soulburn detonates.
/// </summary>
public interface ISoulburnDetonatedListener
{
    Task OnSoulburnDetonatedAsync(PlayerChoiceContext choiceContext, Creature target, int damage);
}

/// <summary>
/// Shared Downfall Soulburn debuff (Hexaghost &amp; Slime Boss's Ghostflame Slime).
/// Starts with a 3-turn countdown; at the start of the owner's turn when countdown == 1,
/// detonates for unblockable HP loss equal to Amount and removes itself.
/// </summary>
public sealed class SoulburnPower : DownfallPower, IHasSecondAmount
{
    public const int DefaultTurns = 3;

    private int _turnsRemaining = DefaultTurns;

    protected override string CharacterKey => "core";
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int TurnsRemaining
    {
        get => _turnsRemaining;
        set
        {
            AssertMutable();
            _turnsRemaining = Math.Max(1, value);
            DynamicVars["Turns"].BaseValue = _turnsRemaining;
            this.InvokeSecondAmountChanged();
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Turns", DefaultTurns)
    ];

    public string GetSecondAmount() => _turnsRemaining.ToString();

    public override IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (context.Creature != Owner || _turnsRemaining != 1 || Amount <= 0)
            return [];

        return
        [
            new HealthBarForecastSegment(
                Amount,
                new Color(0.53f, 0.92f, 0.0f, 1.0f),
                HealthBarForecastDirection.FromRight)
        ];
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (_turnsRemaining <= 1)
        {
            await DetonateAsync(choiceContext);
        }
        else
        {
            TurnsRemaining = _turnsRemaining - 1;
        }
    }

    public async Task DetonateAsync(PlayerChoiceContext choiceContext, bool leaveOne = false)
    {
        int damage = leaveOne ? Amount - 1 : Amount;
        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding || damage <= 0)
            return;

        var target = Owner;
        var dealer = Applier ?? Owner;
        var combatState = target.CombatState;
        Flash();

        if (leaveOne)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, 1 - Amount, dealer, null);
        }
        else
        {
            await PowerCmd.Remove(this);
        }

        if (damage > 0 && !CombatManager.Instance.IsOverOrEnding)
        {
            var aoeModifiers = target.Powers.OfType<ISoulburnAoeModifier>().ToList();
            if (aoeModifiers.Count > 0 && combatState is not null)
            {
                var enemies = combatState.GetTeammatesOf(target).Where(c => c.IsAlive).ToList();
                if (enemies.Count > 0)
                {
                    await CreatureCmd.Damage(
                        choiceContext,
                        enemies,
                        (decimal)damage,
                        ValueProp.Unblockable | ValueProp.Unpowered,
                        dealer);
                }

                foreach (var mod in aoeModifiers)
                {
                    if (!CombatManager.Instance.IsOverOrEnding)
                        await mod.OnSoulburnAoeDetonatedAsync(choiceContext, target);
                }
            }
            else if (target.IsAlive)
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    damage,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    dealer);
            }
        }

        if (combatState is not null && !CombatManager.Instance.IsOverOrEnding)
        {
            var listeners = combatState.Allies
                .Where(c => c.IsAlive)
                .SelectMany(c => c.Powers)
                .OfType<ISoulburnDetonatedListener>()
                .ToList();

            foreach (var listener in listeners)
            {
                if (!CombatManager.Instance.IsOverOrEnding)
                    await listener.OnSoulburnDetonatedAsync(choiceContext, target, damage);
            }
        }
    }
}
