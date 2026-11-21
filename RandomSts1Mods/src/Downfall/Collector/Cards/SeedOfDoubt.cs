using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Seed of Doubt (Uncommon, 0 cost, Skill):
/// Apply 3 Doom. Whenever another card is Exhausted while this is in your hand, increase its Doom by 1 (2).
/// </summary>
public sealed class SeedOfDoubt() : CollectorCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<CollectorDoomPower>(3m),
        new IntVar("DoomGrowth", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DoomHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await CollectorMechanics.ApplyDoomAsync(
            choiceContext,
            target,
            DynamicVars["CollectorDoomPower"].BaseValue,
            Owner.Creature,
            this);
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card != this
            && Owner is not null
            && card.Owner == Owner
            && Pile?.Type == PileType.Hand
            && !CombatManager.Instance.IsOverOrEnding)
        {
            DynamicVars["CollectorDoomPower"].BaseValue += DynamicVars["DoomGrowth"].BaseValue;
        }
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DoomGrowth"].UpgradeValueBy(1m);
    }
}
