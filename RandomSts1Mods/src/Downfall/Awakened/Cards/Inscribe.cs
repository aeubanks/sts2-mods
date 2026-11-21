using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Awakened.Cards;

public sealed class Inscribe()
    : AwakenedCard(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    public override bool UsesConjure => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Copies", 2m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        if (IsUpgraded)
        {
            await AwakenedSpellbook.ConjureAsync(Owner, CombatState);
        }

        await AwakenedSpellbook.ChooseSpellToInscribeAsync(
            choiceContext,
            Owner,
            CombatState,
            SelectionScreenPrompt,
            DynamicVars["Copies"].IntValue);
    }

    protected override void OnUpgrade()
    {
    }
}
