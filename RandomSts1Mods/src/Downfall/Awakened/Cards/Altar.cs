using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class Altar()
    : AwakenedCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool UsesConjure => true;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.PlayerCombatState is { } pcs && pcs.Hand.Cards.Any(c => !ReferenceEquals(c, this)))
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            var chosen = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                prefs,
                c => !ReferenceEquals(c, this),
                this)).FirstOrDefault();

            if (chosen is not null)
            {
                await CardCmd.Exhaust(choiceContext, chosen);
            }
        }

        if (CombatState is not null)
        {
            await AwakenedSpellbook.ConjureAsync(Owner, CombatState);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
