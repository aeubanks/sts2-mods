using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class TornadoPunch()
    : ChampCard(2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    public override bool IsCombo => true;
    public override bool GainsBlock => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && ChampStances.IsInDefensive(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move),
        new BlockVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ComboHoverTip,
        DefensiveHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .WithHitFx("vfx/vfx_attack_blunt", null, "heavy_attack.mp3")
            .Execute(choiceContext);

        if (!ChampStances.IsInDefensive(Owner.Creature))
            return;

        int hits = attack.Results.Sum(r => r.Count);
        for (int i = 0; i < hits; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
