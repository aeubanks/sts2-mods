using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class Nope() : SneckoCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (CombatState is null)
            return;

        var hand = PileType.Hand.GetPile(Owner).Cards;
        if (hand.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, _ => true, this)).FirstOrDefault();
        if (chosen is null)
            return;

        var pool = chosen.Pool;
        bool validClassCard = chosen.Type is CardType.Attack or CardType.Skill or CardType.Power
            && chosen.Rarity != CardRarity.Token;

        await CardCmd.Exhaust(choiceContext, chosen);

        if (!validClassCard)
            return;

        var candidates = pool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && c.Type is CardType.Attack or CardType.Skill or CardType.Power
                && c.CanBeGeneratedInCombat)
            .ToList();

        if (candidates.Count == 0)
            return;

        var canonical = candidates[Owner.RunState.Rng.CombatCardGeneration.NextInt(candidates.Count)];
        var generated = CombatState.CreateCard(canonical, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
