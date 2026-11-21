using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Warlock;

public sealed class Doomguard()
    : PackmasterCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "doomguard";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(21m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        var candidates = PileType.Hand.GetPile(Owner).Cards.ToList();
        var picks = new List<CardModel>(2);
        for (int i = 0; i < 2 && candidates.Count > 0; i++)
        {
            var chosen = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
            if (chosen is null)
                break;
            candidates.Remove(chosen);
            picks.Add(chosen);
        }

        foreach (var chosen in picks)
        {
            if (chosen.Pile?.Type == PileType.Hand)
            {
                await CardCmd.Exhaust(choiceContext, chosen);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
    }
}
