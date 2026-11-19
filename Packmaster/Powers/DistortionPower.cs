using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class DistortionPower : PackmasterPower
{
    protected override string IconKey => "distortion_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (!Owner.IsAlive || Amount <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        int damage = Amount;
        Flash();
        await PowerCmd.Decrement(this);

        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }
}
