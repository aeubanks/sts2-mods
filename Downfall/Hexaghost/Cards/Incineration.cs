using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Deal 4 damage and apply 4 Soulburn 3 (4) times.
/// </summary>
public sealed class Incineration() : HexaCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new IntVar("Soulburn", 4m),
        new IntVar("Hits", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = DynamicVars["Hits"].IntValue;
        int burn = DynamicVars["Soulburn"].IntValue;

        for (int i = 0; i < hits; i++)
        {
            if (!cardPlay.Target.IsAlive || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (cardPlay.Target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await ApplySoulburnAsync(choiceContext, cardPlay.Target, burn);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m);
    }
}
