using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Awakened.Cards;

public sealed class MysticOrder()
    : AwakenedCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool UsesConjure => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        if (CombatState is not null)
        {
            await AwakenedSpellbook.ConjureAsync(Owner, CombatState);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
