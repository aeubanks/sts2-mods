using Downfall.Hermit.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Relics;

public sealed class OldLocket : HermitRelic
{
    protected override string IconKey => "old_locket";

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MementoCard>()
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || combatState.RoundNumber != 1 || combatState is not CombatState cs)
            return;

        Flash();
        var memento = cs.CreateCard<MementoCard>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(memento, PileType.Hand, Owner);
    }
}
