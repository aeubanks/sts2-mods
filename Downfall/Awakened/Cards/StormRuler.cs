using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Awakened.Cards;

public sealed class StormRuler()
    : AwakenedCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    public override bool UsesConjure => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BonusDamage", 6m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<Thunderbolt>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null)
        {
            await AwakenedSpellbook.ConjureAsync(Owner, CombatState);
        }

        await PowerCmd.Apply<GrimoirePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["BonusDamage"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BonusDamage"].UpgradeValueBy(3m);
    }
}
