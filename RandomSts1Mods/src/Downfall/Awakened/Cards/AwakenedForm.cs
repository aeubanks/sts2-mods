using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Cards;

public sealed class AwakenedForm()
    : AwakenedCard(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<AwakenedCuriosityPower>(1m),
        new PowerVar<RitualPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<AwakenedCuriosityPower>(),
        HoverTipFactory.FromPower<RitualPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded && CombatState is not null)
        {
            await AwakenedSpellbook.TryCheckAwakeningAsync(CombatState, Owner, choiceContext, force: true);
        }

        int curiosityGain = DynamicVars[nameof(AwakenedCuriosityPower)].IntValue;
        var curiosity = await PowerCmd.Apply<AwakenedCuriosityPower>(
            choiceContext,
            Owner.Creature,
            curiosityGain,
            Owner.Creature,
            this);
        curiosity?.IgnoreAmountForPlay(cardPlay, curiosityGain);

        await PowerCmd.Apply<RitualPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(RitualPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(RitualPower)].UpgradeValueBy(1m);
    }
}
