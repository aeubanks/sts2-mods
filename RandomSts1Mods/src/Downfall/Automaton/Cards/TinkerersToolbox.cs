using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Dev Tools (TinkerersToolbox, Rare, 1 -> 0 cost, Retain, Exhaust):
/// Choose Debug, Batch, Decompile, or Byte Shift to add to your Hand.
/// </summary>
public sealed class TinkerersToolbox() : BronzeCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Debug>(),
        HoverTipFactory.FromCard<Batch>(),
        HoverTipFactory.FromCard<Decompile>(),
        HoverTipFactory.FromCard<ByteShift>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        List<CardModel> choices =
        [
            combatState.CreateCard<Debug>(owner),
            combatState.CreateCard<Batch>(owner),
            combatState.CreateCard<Decompile>(owner),
            combatState.CreateCard<ByteShift>(owner)
        ];

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, choices, owner, prefs)).FirstOrDefault();
        if (chosen is null || !owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(chosen, owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
