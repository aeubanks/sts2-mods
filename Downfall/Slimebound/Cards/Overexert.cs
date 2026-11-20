using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Cards;

public sealed class Overexert() : SlimeCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PotencyPower>(5m),
        new PowerVar<LoseSlimesPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [PotencyHoverTip, CommandHoverTip, AbsorbHoverTip]
            : [PotencyHoverTip, AbsorbHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<PotencyPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["PotencyPower"].BaseValue,
            Owner.Creature,
            this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<LoseSlimesPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars["LoseSlimesPower"].BaseValue,
                Owner.Creature,
                this);
        }

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.CommandAsync(choiceContext, Owner, count: 2);
        }
    }
}
