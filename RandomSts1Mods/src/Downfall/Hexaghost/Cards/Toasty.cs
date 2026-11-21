using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Flare Flick (Toasty): Deal 10 (14) damage. Ignite the active Ghostflame and Advance.
/// Upgraded: Choose to Retract, Advance, or do nothing instead of always Advancing.
/// </summary>
public sealed class Toasty() : HexaCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [IgniteHoverTip, AdvanceHoverTip, RetractHoverTip, GhostflameHoverTip]
            : [IgniteHoverTip, AdvanceHoverTip, GhostflameHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await HexaMechanics.IgniteActiveAsync(choiceContext, Owner, extinguishFirstIfIgnited: true);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (IsUpgraded)
            await HexaMechanics.ChooseMoveGhostflameAsync(choiceContext, Owner);
        else
            await HexaMechanics.AdvanceAsync(choiceContext, Owner, isEndTurn: false);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
