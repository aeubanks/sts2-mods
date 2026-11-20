using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Ouroboros (DownfallSTS CollectorCard, Collectible, 0 cost, Skill, Pyre, Exhaust):
/// Apply 1 (2) Weak and 1 (2) Vulnerable. Gain 3 (5) Temp HP.
/// </summary>
public sealed class OuroborosCard() : CollectibleCard(0, CardType.Skill, TargetType.AnyEnemy)
{
    public override bool IsPyre => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m),
        new IntVar("TempHp", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>(),
        TempHpHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is not null)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
            if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CollectorMechanics.GainTempHpAsync(choiceContext, Owner.Creature, DynamicVars["TempHp"].BaseValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Weak.UpgradeValueBy(1m);
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
        DynamicVars["TempHp"].UpgradeValueBy(2m);
    }
}
