using Downfall.Guardian.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class ShieldSpikes() : GuardianCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override bool UsesDefensiveModeGlow => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move),
        new PowerVar<ThornsPower>(3m),
        new IntVar("Brace", 8m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ThornsPower>(),
        DefensiveModeHoverTip,
        BraceHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.Creature.HasPower<DefensiveModePower>()
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<ThornsPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(ThornsPower)].BaseValue,
                Owner.Creature,
                this);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
        DynamicVars[nameof(ThornsPower)].UpgradeValueBy(1m);
    }
}
