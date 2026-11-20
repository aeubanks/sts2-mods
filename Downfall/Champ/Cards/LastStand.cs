using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Cards;

public sealed class LastStand()
    : ChampCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && Owner is not null && LastStandPower.IsBelowHalfHp(Owner.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal strAmount = DynamicVars[nameof(StrengthPower)].BaseValue;
        if (LastStandPower.IsBelowHalfHp(Owner.Creature))
        {
            await LastStandPower.TriggerCleanseAndStrengthAsync(choiceContext, Owner.Creature, strAmount, this);
        }
        else
        {
            await PowerCmd.Apply<LastStandPower>(choiceContext, Owner.Creature, strAmount, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
