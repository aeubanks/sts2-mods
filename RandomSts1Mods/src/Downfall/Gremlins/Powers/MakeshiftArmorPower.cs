using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Gremlins.Powers;

public sealed class MakeshiftArmorPower : GremlinPower
{
    private const int AttacksRequired = 7;
    private const string AttacksLeftKey = "AttacksLeft";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => DynamicVars[AttacksLeftKey].IntValue;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(AttacksLeftKey, AttacksRequired)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ArtifactPower>()
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Attack
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        DynamicVars[AttacksLeftKey].BaseValue--;

        if (DynamicVars[AttacksLeftKey].IntValue <= 0)
        {
            DynamicVars[AttacksLeftKey].BaseValue = AttacksRequired;
            InvokeDisplayAmountChanged();
            Flash();
            await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner, Amount, Owner, null);
        }
        else
        {
            InvokeDisplayAmountChanged();
        }
    }
}
