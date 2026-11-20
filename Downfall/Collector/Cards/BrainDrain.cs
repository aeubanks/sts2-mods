using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Brain Drain (Uncommon, 0 cost, Skill, Exhaust):
/// Enemy loses 6 (7) HP. Add their Collected card into your hand (Upgraded: and Upgrade it).
/// </summary>
public sealed class BrainDrain() : CollectorCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("HpLoss", 6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            target,
            DynamicVars["HpLoss"].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            this,
            cardPlay);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            RoomType roomType = CombatState?.Encounter?.RoomType ?? RoomType.Monster;
            var collected = CollectorMechanics.CreateCollectibleForEnemy(Owner, target, roomType);
            if (IsUpgraded && collected.IsUpgradable)
                collected.UpgradeInternal();
            await CardPileCmd.AddGeneratedCardToCombat(collected, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HpLoss"].UpgradeValueBy(1m);
    }
}
