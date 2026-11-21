using Downfall.Guardian.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class ShieldCharger() : GuardianCard(2, CardType.Skill, CardRarity.Common, TargetType.Self), IInStasisCard
{
    public override bool GainsBlock => true;
    public override bool IsSelfStasis => true;
    public override bool IsVolatile => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new IntVar("Brace", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        StasisHoverTip,
        BraceHoverTip,
        VolatileHoverTip
    ];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    public async Task OnTurnStartInStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["Brace"].UpgradeValueBy(2m);
    }
}
