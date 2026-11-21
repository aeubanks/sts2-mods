using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Refactor (Uncommon, 1 cost):
/// Scry 4. Exhaust all Status cards discarded. Gain 4 (6) Block for each Status card exhausted.
/// </summary>
public sealed class Refactor() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Scry", 4m),
        new BlockVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.Static(StaticHoverTip.Block)
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

        var discarded = await AutomatonMechanics.ScryAsync(
            choiceContext,
            owner,
            DynamicVars["Scry"].IntValue,
            SelectionScreenPrompt);

        foreach (var card in discarded)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (card.Type == CardType.Status && card.Pile?.Type != PileType.Exhaust)
            {
                await GainBlockAsync(owner, cardPlay, isFromPower, DynamicVars.Block.BaseValue);
                if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                    await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
