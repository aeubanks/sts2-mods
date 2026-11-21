using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Bronze Orb (Collectible, 1 cost, Attack, Exhaust):
/// Gain 5 (7) Block. Deal 5 (7) damage. The top card of your Draw Pile costs 0 until played.
/// </summary>
public sealed class BronzeOrbCard() : CollectibleCard(1, CardType.Attack, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new DamageVar(5m, ValueProp.Move)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.PlayerCombatState is not { } pcs || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }

        var topCard = pcs.DrawPile.Cards.FirstOrDefault();
        if (topCard is not null && !topCard.EnergyCost.CostsX)
            topCard.EnergyCost.SetUntilPlayed(0);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
