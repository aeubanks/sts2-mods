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
/// Return (BronzeReturn, Uncommon, 1 cost):
/// Put a card from your Discard Pile on top of your Draw Pile. Next turn, gain 1 (2) Energy.
/// </summary>
public sealed class BronzeReturn() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<EnergyNextTurnPower>()
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

        var discard = PileType.Discard.GetPile(owner);
        if (discard.Cards.Count > 0)
        {
            CardModel? chosen;
            if (discard.Cards.Count == 1)
            {
                chosen = discard.Cards[0];
            }
            else
            {
                var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
                chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, discard.Cards.ToList(), owner, prefs)).FirstOrDefault();
            }

            if (chosen is not null && owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top);
        }

        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<EnergyNextTurnPower>(
                choiceContext,
                owner.Creature,
                DynamicVars.Energy.BaseValue,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
