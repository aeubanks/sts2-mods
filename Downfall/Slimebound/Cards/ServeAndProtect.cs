using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class ServeAndProtect() : SlimeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BlurPower>(),
        AbsorbHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int slimeCount = SlimeMechanics.GetSlimes(Owner).Count;
        if (slimeCount > 0)
        {
            for (int i = 0; i < slimeCount; i++)
            {
                if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            }

            if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, slimeCount, Owner.Creature, this);
            }
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.AbsorbAllSlimesAsync(choiceContext, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5m);
    }
}
