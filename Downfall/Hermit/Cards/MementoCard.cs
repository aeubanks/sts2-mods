using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hermit.Cards;

public sealed class MementoCard()
    : HermitCard(0, CardType.Curse, CardRarity.Token, TargetType.AllEnemies, showInCardLibrary: false)
{
    protected override string PortraitKey => "memento";

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VulnerablePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        decimal vuln = DynamicVars.Vulnerable.BaseValue;

        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, vuln, Owner.Creature, this);
        }

        await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature, vuln, Owner.Creature, this);
    }
}
