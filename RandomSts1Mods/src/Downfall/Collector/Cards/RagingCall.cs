using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Raging Call (Uncommon, 2 cost, Power):
/// Gain 6 (7) Temp HP. Torch Head gains "Deal 3 (5) damage to ALL enemies."
/// </summary>
public sealed class RagingCall() : CollectorCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TempHp", 6m),
        new IntVar("TorchAoe", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TempHpHoverTip,
        TorchHeadHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CollectorMechanics.GainTempHpAsync(choiceContext, Owner.Creature, DynamicVars["TempHp"].BaseValue, this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await TorchHeadPower.AddChannelAsync(
                choiceContext,
                Owner.Creature,
                TorchHeadChannel.AoeDamage,
                DynamicVars["TorchAoe"].IntValue,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TempHp"].UpgradeValueBy(1m);
        DynamicVars["TorchAoe"].UpgradeValueBy(2m);
    }
}
