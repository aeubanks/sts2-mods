using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Cards;

public sealed class GremlinArms()
    : GremlinCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RepeatVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SwapHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        if (!GremlinMob.IsNob(CombatState, Owner))
        {
            GremlinKind kind = GremlinMob.GetActiveKind(CombatState, Owner);
            await GremlinMob.TriggerBonusAsync(Owner, CombatState, choiceContext, kind, DynamicVars.Repeat.IntValue);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.ChooseGremlinToSwapAsync(Owner, CombatState, choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
