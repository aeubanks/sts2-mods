using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class SetATrap()
    : ChampCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool IsCombo => true;
    public override bool GainsBlock => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && ChampStances.IsInDefensive(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new PowerVar<WeakPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ComboHoverTip,
        DefensiveHoverTip,
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (!Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || !ChampStances.IsInDefensive(Owner.Creature))
        {
            return;
        }

        foreach (var enemy in CombatState!.HittableEnemies.ToList())
        {
            if (!enemy.IsAlive || CombatManager.Instance.IsOverOrEnding)
                continue;

            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                enemy,
                DynamicVars[nameof(WeakPower)].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars[nameof(WeakPower)].UpgradeValueBy(1m);
    }
}
