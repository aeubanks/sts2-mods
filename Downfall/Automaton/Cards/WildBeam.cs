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
/// Cleanse (WildBeam, Common, 1 cost):
/// Deal 10 (14) damage. Exhaust a random Status card in your Draw Pile.
/// </summary>
public sealed class WildBeam() : BronzeCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);

        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var statuses = PileType.Draw.GetPile(owner).Cards
            .Where(c => c.Type == CardType.Status)
            .ToList();
        if (statuses.Count > 0)
        {
            var chosen = owner.RunState.Rng.CombatCardSelection.NextItem(statuses);
            if (chosen is not null)
                await CardCmd.Exhaust(choiceContext, chosen, causedByEthereal: false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
