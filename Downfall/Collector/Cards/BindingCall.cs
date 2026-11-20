using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Binding Call (Uncommon, 2 cost, Power):
/// Gain 6 (8) Temp HP. Torchhead gains "Apply 2 (3) Doom to a random enemy."
/// </summary>
public sealed class BindingCall() : CollectorCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TempHp", 6m),
        new IntVar("TorchDoom", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TempHpHoverTip,
        TorchHeadHoverTip,
        DoomHoverTip
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
                TorchHeadChannel.RandomDoom,
                DynamicVars["TorchDoom"].IntValue,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TempHp"].UpgradeValueBy(2m);
        DynamicVars["TorchDoom"].UpgradeValueBy(1m);
    }
}
