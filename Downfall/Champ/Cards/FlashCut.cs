using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class FlashCut()
    : ChampCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool IsCombo => true;
    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && ChampStances.IsInDefensive(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new PowerVar<CounterPower>(4m),
        new BlockVar(5m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ComboHoverTip,
        DefensiveHoverTip,
        HoverTipFactory.FromPower<CounterPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, "slash_attack.mp3")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || !ChampStances.IsInDefensive(Owner.Creature))
        {
            return;
        }

        await PowerCmd.Apply<CounterPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(CounterPower)].BaseValue,
            Owner.Creature,
            this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars[nameof(CounterPower)].UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
