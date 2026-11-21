using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Assembly (Rare, 1 cost, Exhaust):
/// Scry 5 (8). Encode all Encodable cards discarded (in selected order).
/// </summary>
public sealed class Assembly() : BronzeCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Scry", 5m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var chosen = await AutomatonMechanics.ScryAsync(
            choiceContext,
            owner,
            DynamicVars["Scry"].IntValue,
            SelectionScreenPrompt,
            discardSelected: false);

        foreach (var card in chosen)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (card is BronzeCard { Encodes: true } encodable)
            {
                await AutomatonMechanics.EncodeCardAsync(choiceContext, owner, encodable);
            }
            else
            {
                await CardPileCmd.Add(card, PileType.Discard);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Scry"].UpgradeValueBy(3m);
    }
}
