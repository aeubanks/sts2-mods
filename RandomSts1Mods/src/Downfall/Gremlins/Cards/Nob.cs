using Downfall.Core;
using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Cards;

public sealed class Nob()
    : GremlinCard(4, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DownfallTempHpPower>(20m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TempHpHoverTip,
        HoverTipFactory.FromCard<Bellow>(),
        HoverTipFactory.FromCard<SkullBash>(),
        HoverTipFactory.FromCard<Rush>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DownfallTempHpPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(DownfallTempHpPower)].BaseValue,
            Owner.Creature,
            this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<GremlinNobPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(DownfallTempHpPower)].UpgradeValueBy(10m);
    }
}
