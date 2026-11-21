using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Ignite (Bronze Idol Good Status for Burn, 1 cost):
/// Apply 10 (15) Soulburn. At the end of your turn in Hand, take 2 (4) damage.
/// </summary>
public sealed class BronzeIgnite() : BronzeGoodStatusCard(1, TargetType.AnyEnemy)
{
    public override bool HasTurnEndInHandEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SoulburnPower>(10m),
        new DamageVar("SelfDamage", 2m, ValueProp.Unpowered | ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SoulburnPower>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var resolved = ResolveTarget(owner, target);
        if (resolved is null)
            return;

        await PowerCmd.Apply<SoulburnPower>(
            choiceContext,
            resolved,
            DynamicVars[nameof(SoulburnPower)].BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            (DamageVar)DynamicVars["SelfDamage"],
            this,
            null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(SoulburnPower)].UpgradeValueBy(5m);
        DynamicVars["SelfDamage"].UpgradeValueBy(2m);
    }
}
