using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Jar of TOBSCo (InflammatoryLetter): Start each combat with 1 Intensity.
/// </summary>
public sealed class InflammatoryLetter : HexaRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<EnhancePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IntensityHoverTip
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Creature.Side
            || !participants.Contains(Owner.Creature)
            || combatState.RoundNumber != 1
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<EnhancePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(EnhancePower)].BaseValue,
            Owner.Creature,
            null);
    }
}
