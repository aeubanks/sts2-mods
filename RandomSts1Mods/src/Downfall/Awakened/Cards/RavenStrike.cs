using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class RavenStrike()
    : AwakenedCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool UsesChant => true;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(15m, ValueProp.Move)
    ];

    private bool IsPotentialSoftlock()
    {
        if (CombatState is null || Owner.PlayerCombatState is not { } pcs)
            return false;

        if (pcs.DrawPile.Cards.Count > 0)
            return false;

        int playsThisTurn = CombatManager.Instance.History.CardPlaysStarted.Count(e =>
            e.HappenedThisTurn(CombatState) && e.CardPlay.Card.Owner == Owner);
        if (playsThisTurn < 50)
            return false;

        return pcs.DiscardPile.Cards.All(c => c is RavenStrike);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        int chantTriggers = TryTriggerChantCount(cardPlay);
        for (int i = 0; i < chantTriggers; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive || IsPotentialSoftlock())
                break;

            await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, 1, CardPilePosition.Top, forceExhaust: false);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
