using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Mutator (Rare, 1 cost, Power; upgraded gains Retain):
/// Gain 2 Strength. Transform a Status in your Hand into a copy of this (Exhausts the Status).
/// </summary>
public sealed class Mutator() : BronzeCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.PlayerCombatState is not { } pcs
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            owner.Creature,
            DynamicVars.Strength.BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);

        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var statusesInHand = pcs.Hand.Cards
            .Where(c => c.Type == CardType.Status && !ReferenceEquals(c, this))
            .ToList();
        if (statusesInHand.Count == 0)
            return;

        CardModel? chosen;
        if (statusesInHand.Count == 1)
        {
            chosen = statusesInHand[0];
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, statusesInHand, owner, prefs)).FirstOrDefault();
        }

        if (chosen is null || !owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CardCmd.Exhaust(choiceContext, chosen);

        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var copy = CreateClone();
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(copy, owner);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
