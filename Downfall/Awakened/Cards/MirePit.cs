using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Cards;

public sealed class MirePit()
    : AwakenedCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthLoss", 6m),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<HermitDrainedPower>(),
        HoverTipFactory.ForEnergy(this)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null)
        {
            decimal loss = DynamicVars["StrengthLoss"].BaseValue;
            foreach (var enemy in CombatState.HittableEnemies.ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!enemy.IsAlive)
                    continue;

                await PowerCmd.Apply<PiercingWailPower>(
                    choiceContext,
                    enemy,
                    loss,
                    Owner.Creature,
                    this);
            }
        }

        await PowerCmd.Apply<HermitDrainedPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Energy.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["StrengthLoss"].UpgradeValueBy(2m);
    }
}
