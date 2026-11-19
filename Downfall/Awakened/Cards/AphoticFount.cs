using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Cards;

public sealed class AphoticFount()
    : AwakenedCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public override bool UsesConjure => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PlatingPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<Cryostasis>(),
        HoverTipFactory.FromPower<PlatingPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null)
        {
            await AwakenedSpellbook.ConjureAsync(Owner, CombatState);
        }

        await PowerCmd.Apply<AphoticFountPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(PlatingPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(PlatingPower)].UpgradeValueBy(1m);
    }
}
