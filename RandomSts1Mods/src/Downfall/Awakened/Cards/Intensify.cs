using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Awakened.Cards;

public sealed class Intensify()
    : AwakenedCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool UsesConjure => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null)
        {
            await AwakenedSpellbook.ConjureAsync(Owner, CombatState);
        }

        await PowerCmd.Apply<IntensifyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<IntensifyDebuffPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
