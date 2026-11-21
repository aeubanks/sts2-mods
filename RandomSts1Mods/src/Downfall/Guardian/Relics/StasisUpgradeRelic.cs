using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Guardian.Relics;

public sealed class StasisUpgradeRelic : GuardianRelic, IStasisUpgradeProvider
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public bool ShouldUpgradeOnStasis => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.StasisHoverTip
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Creature.Side
            || combatState.RoundNumber != 1
            || !participants.Contains(Owner.Creature)
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await OrbCmd.AddSlots(Owner, 1);
    }
}
