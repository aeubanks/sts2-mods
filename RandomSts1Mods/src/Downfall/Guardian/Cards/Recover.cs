using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class Recover() : GuardianCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new IntVar("Brace", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BraceHoverTip,
        StasisHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }

        var discard = PileType.Discard.GetPile(Owner).Cards.ToList();
        if (discard.Count > 0
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && GuardianMechanics.CanPlaceInStasis(Owner))
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, discard, Owner, prefs)).FirstOrDefault();
            if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, chosen);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["Brace"].UpgradeValueBy(1m);
    }
}
