using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class WaveOfMiasma()
    : AwakenedCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override bool GainsBlock => true;

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new PowerVar<ManaburnPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<ManaburnPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (CombatState is null)
            return;

        decimal manaburn = DynamicVars[nameof(ManaburnPower)].BaseValue;
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;

            await PowerCmd.Apply<ManaburnPower>(
                choiceContext,
                enemy,
                manaburn,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars[nameof(ManaburnPower)].UpgradeValueBy(2m);
    }
}
