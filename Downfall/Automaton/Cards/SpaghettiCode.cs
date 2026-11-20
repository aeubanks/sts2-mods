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
/// Spaghetti Code (Rare, 2 -> 1 cost, Exhaust):
/// Choose 1 of 3 random cards to Encode. Repeat until you create a Function.
/// </summary>
public sealed class SpaghettiCode() : BronzeCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

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
        if (owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var state = AutomatonCombatState.Get(combatState);
        int startCompiled = state.FunctionsCompiledThisCombat;

        for (int safety = 0; safety < AutomatonMechanics.FunctionCapacity * 2; safety++)
        {
            if (!owner.Creature.IsAlive
                || CombatManager.Instance.IsOverOrEnding
                || state.FunctionsCompiledThisCombat > startCompiled)
            {
                break;
            }

            var choices = AutomatonMechanics.CreateDistinctRandomEncodableChoices(owner, 3)
                .Cast<CardModel>()
                .ToList();
            var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, owner, canSkip: false);
            if (chosen is not BronzeCard bronze || !owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await AutomatonMechanics.EncodeCardAsync(choiceContext, owner, bronze);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
