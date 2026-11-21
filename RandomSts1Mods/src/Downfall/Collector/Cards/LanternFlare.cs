using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Lantern Flare (Uncommon, 2 cost, Skill, Pyre):
/// Apply 12 (15) Doom. Enemy gains 3 (4) more Doom at the end of each of its turns.
/// </summary>
public sealed class LanternFlare() : CollectorCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsPyre => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<CollectorDoomPower>(12m),
        new PowerVar<LanternFlarePower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
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

        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<LanternFlarePower>(
                choiceContext,
                target,
                DynamicVars["LanternFlarePower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CollectorDoomPower"].UpgradeValueBy(3m);
        DynamicVars["LanternFlarePower"].UpgradeValueBy(1m);
    }
}
