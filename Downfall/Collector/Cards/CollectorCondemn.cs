using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards;

/// <summary>
/// Condemn (Common, 1 cost, Skill):
/// Apply 1 (2) Vulnerable. Apply 5 (6) Doom.
/// </summary>
public sealed class CollectorCondemn() : CollectorCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VulnerablePower>(1m),
        new PowerVar<CollectorDoomPower>(5m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        DoomHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CollectorMechanics.ApplyDoomAsync(choiceContext, target, DynamicVars["CollectorDoomPower"].BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
        DynamicVars["CollectorDoomPower"].UpgradeValueBy(1m);
    }
}
