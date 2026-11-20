using Downfall.Core;
using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Radiant Reverb (RadiantFlame): Deal 18 (24) damage.
/// Remove the target's Soulburn to gain 2 (3) Intensity.
/// </summary>
public sealed class RadiantFlame() : HexaCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move),
        new PowerVar<EnhancePower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip,
        IntensityHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var soulburn = cardPlay.Target.GetPower<SoulburnPower>();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (soulburn is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            if (cardPlay.Target.IsAlive && cardPlay.Target.Powers.Contains(soulburn))
                await PowerCmd.Remove(soulburn);

            if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<EnhancePower>(
                    choiceContext,
                    Owner.Creature,
                    DynamicVars[nameof(EnhancePower)].BaseValue,
                    Owner.Creature,
                    this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars[nameof(EnhancePower)].UpgradeValueBy(1m);
    }
}
