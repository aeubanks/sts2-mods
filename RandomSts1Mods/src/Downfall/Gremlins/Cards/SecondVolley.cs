using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Cards;

public sealed class SecondVolley()
    : GremlinCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const string CalculatedCardsKey = "CalculatedCards";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar(CalculatedCardsKey).WithMultiplier((card, _) =>
            card.CombatState is { } cs
                ? CombatManager.Instance.History.CardPlaysFinished.Count(e =>
                    e.HappenedThisTurn(cs) && e.CardPlay.Player == card.Owner)
                : 0)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Shiv>(IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int played = (int)((CalculatedVar)DynamicVars[CalculatedCardsKey]).Calculate(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (played <= 0 || CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int availableSpace = Math.Max(0, CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count);
        int count = Math.Min(played, availableSpace);
        for (int i = 0; i < count; i++)
        {
            var shiv = CombatState.CreateCard<Shiv>(Owner);
            if (IsUpgraded)
                CardCmd.Upgrade(shiv);
            await CardPileCmd.AddGeneratedCardToCombat(shiv, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
