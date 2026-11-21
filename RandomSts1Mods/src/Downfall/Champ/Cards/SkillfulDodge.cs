using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class SkillfulDodge()
    : ChampCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const string IncreaseKey = "Increase";

    public override bool IsCombo => true;
    public override bool GainsBlock => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && ChampStances.IsInDefensive(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move),
        new PowerVar<CounterPower>(4m),
        new DynamicVar(IncreaseKey, 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ComboHoverTip,
        DefensiveHoverTip,
        HoverTipFactory.FromPower<CounterPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<CounterPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(CounterPower)].BaseValue,
                Owner.Creature,
                this);
        }

        if (Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && ChampStances.IsInDefensive(Owner.Creature))
        {
            decimal inc = DynamicVars[IncreaseKey].BaseValue;
            DynamicVars.Block.BaseValue += inc;
            DynamicVars[nameof(CounterPower)].BaseValue += inc;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars[nameof(CounterPower)].UpgradeValueBy(1m);
        DynamicVars[IncreaseKey].UpgradeValueBy(1m);
    }
}
