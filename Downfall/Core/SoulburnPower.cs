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

    public async Task DetonateAsync(PlayerChoiceContext choiceContext)
    {
        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding || Amount <= 0)
            return;

        int damage = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (damage > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner,
                damage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Applier ?? Owner);
        }
    }
}
