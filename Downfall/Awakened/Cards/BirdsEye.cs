using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Awakened.Cards;

public sealed class BirdsEye()
    : AwakenedCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool UsesConjure => true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        if (IsUpgraded)
        {
            AwakenedSpellbook.RefreshSpells(CombatState, Owner);
        }

        await AwakenedSpellbook.ChooseSpellToConjureAsync(choiceContext, Owner, CombatState, SelectionScreenPrompt);
    }

    protected override void OnUpgrade()
    {
    }
}
