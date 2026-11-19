using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Awakened.Powers;

public sealed class SongOfSorrowPower : AwakenedPower
{
    protected override string IconKey => "song_of_sorrow_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<StsCards.Void>()
    ];

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is not StsCards.Void
            || creator != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        var ctx = new ThrowingPlayerChoiceContext();
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;

            await CreatureCmd.Damage(
                ctx,
                enemy,
                Amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: Owner,
                cardSource: null,
                cardPlay: null);
        }
    }
}
