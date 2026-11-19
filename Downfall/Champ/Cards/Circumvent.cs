using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class Circumvent()
    : ChampCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool IsCombo => true;
    public override bool GainsBlock => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && ChampStances.IsInDefensive(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ComboHoverTip,
        DefensiveHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        if (!Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || ChampStances.IsInDefensive(Owner.Creature))
        {
            return;
        }

        int count = Math.Min(DynamicVars.Cards.IntValue, PileType.Hand.GetPile(Owner).Cards.Count(c => !ReferenceEquals(c, this)));
        if (count <= 0)
            return;

        var prefs = new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, count);
        var chosen = (await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            Owner,
            prefs,
            c => !ReferenceEquals(c, this),
            this)).ToList();

        if (chosen.Count > 0)
        {
            await CardCmd.Discard(choiceContext, chosen);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
