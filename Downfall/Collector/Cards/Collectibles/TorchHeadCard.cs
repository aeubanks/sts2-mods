using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Torch Head (TorchHeadCard, Collectible, 1 -> 0 cost, Skill, Exhaust):
/// Add a random Torch Head Call (Binding Call, Protecting Call, or Raging Call) into your hand. It costs 0 this turn.
/// </summary>
public sealed class TorchHeadCard() : CollectibleCard(1, CardType.Skill, TargetType.Self)
{
    private static readonly Type[] TorchHeadCalls =
    [
        typeof(BindingCall),
        typeof(ProtectingCall),
        typeof(RagingCall)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TorchHeadHoverTip,
        HoverTipFactory.FromCard<BindingCall>(),
        HoverTipFactory.FromCard<ProtectingCall>(),
        HoverTipFactory.FromCard<RagingCall>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var chosenType = TorchHeadCalls[rng.NextInt(TorchHeadCalls.Length)];
        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(chosenType));
        var card = CombatState.CreateCard(canonical, Owner);
        card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
