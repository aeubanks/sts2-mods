using Downfall.Slimebound.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class PreparingCrushPower : SlimePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SlimeCrush>()
    ];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        int count = Amount;
        Flash();
        await PowerCmd.Remove(this);
        for (int i = 0; i < count; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var crush = combatState.CreateCard<SlimeCrush>(player);
            await CardPileCmd.AddGeneratedCardToCombat(crush, PileType.Hand, player);
        }
    }
}
