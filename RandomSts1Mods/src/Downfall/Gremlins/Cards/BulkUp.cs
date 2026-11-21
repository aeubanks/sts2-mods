using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Cards;

public sealed class BulkUp()
    : GremlinCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DownfallTempHpPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TempHpHoverTip,
        SwapHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DownfallTempHpPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(DownfallTempHpPower)].BaseValue,
            Owner.Creature,
            this);

        if (CombatState is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapToSpecificAsync(Owner, CombatState, GremlinKind.Fat, choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(DownfallTempHpPower)].UpgradeValueBy(2m);
    }
}
