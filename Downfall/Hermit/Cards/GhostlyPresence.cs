using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class GhostlyPresence()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "ghostly_presence";

    public override bool UsesDeadOn => true;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new PowerVar<WeakPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..base.ExtraHoverTips,
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        int triggers = await ConsumeDeadOnTriggersAsync(cardPlay);
        decimal weak = DynamicVars.Weak.BaseValue;
        for (int i = 0; i < triggers; i++)
        {
            foreach (var enemy in CombatState.HittableEnemies.ToList())
            {
                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, weak, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Weak.UpgradeValueBy(1m);
    }
}
