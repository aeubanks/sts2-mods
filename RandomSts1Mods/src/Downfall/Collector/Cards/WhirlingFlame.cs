using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Whirling Flame (Uncommon, 2 cost, Attack):
/// Deal 15 (19) damage to ALL enemies. Put a card from your Discard Pile on top of your Draw Pile.
/// </summary>
public sealed class WhirlingFlame() : CollectorCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(15m, ValueProp.Move)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var discard = PileType.Discard.GetPile(Owner);
        if (discard.Cards.Count == 0)
            return;

        CardModel? chosen;
        if (discard.Cards.Count == 1)
        {
            chosen = discard.Cards[0];
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, discard.Cards.ToList(), Owner, prefs)).FirstOrDefault();
        }

        if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
