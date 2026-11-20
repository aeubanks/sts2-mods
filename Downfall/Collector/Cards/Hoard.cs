using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Hoard (Uncommon, 1 cost, Skill, Pyre):
/// Draw 2 (3) cards. This turn, they Retain.
/// </summary>
public sealed class Hoard() : CollectorCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool IsPyre => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var drawn = await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        foreach (var card in drawn)
        {
            card.GiveSingleTurnRetain();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
