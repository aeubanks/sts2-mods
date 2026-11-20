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
/// Karma (Uncommon, 1 cost, Power):
/// At the end of your turn, gain 2 (3) Block, plus 2 (3) more for each Afflicted enemy.
/// </summary>
public sealed class Karma() : CollectorCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<KarmaPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfflictedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<KarmaPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["KarmaPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["KarmaPower"].UpgradeValueBy(1m);
    }
}
