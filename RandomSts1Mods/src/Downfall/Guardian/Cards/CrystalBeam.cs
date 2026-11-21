using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class CrystalBeam() : GuardianCard(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool IsBeam => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(12m),
        new ExtraDamageVar(2m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(CountGemsInDeck)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [GemHoverTip];

    private static decimal CountGemsInDeck(CardModel card, Creature? _)
    {
        if (card.Owner?.Deck is not { } deck)
            return 0m;

        int count = 0;
        foreach (var c in deck.Cards)
        {
            if (c is GuardianCard gc)
            {
                if (gc.IsGem)
                    count++;
                count += gc.GetSocketedGems().Count;
            }
        }
        return count;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(4m);
        DynamicVars.ExtraDamage.UpgradeValueBy(1m);
    }
}
