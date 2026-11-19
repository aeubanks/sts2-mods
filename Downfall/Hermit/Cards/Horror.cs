using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class Horror()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string PortraitKey => "horror";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BruisePower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BruisePower>(),
        HoverTipFactory.FromPower<HorrorPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        decimal bruise = DynamicVars[nameof(BruisePower)].BaseValue;
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await PowerCmd.Apply<BruisePower>(choiceContext, enemy, bruise, Owner.Creature, this);
            await PowerCmd.Apply<HorrorPower>(choiceContext, enemy, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(BruisePower)].UpgradeValueBy(2m);
    }
}
