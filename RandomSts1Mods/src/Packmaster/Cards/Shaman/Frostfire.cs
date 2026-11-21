using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Packmaster.Powers;

namespace Packmaster.Cards.Shaman;

public sealed class Frostfire()
    : PackmasterCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string PortraitKey => "frostfire";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<IgnitePower>(1m),
        new PowerVar<WeakPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IgnitePower>(),
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        decimal ignite = DynamicVars[nameof(IgnitePower)].BaseValue;
        decimal weak = DynamicVars.Weak.BaseValue;
        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            if (!enemy.IsAlive)
                continue;

            await PowerCmd.Apply<IgnitePower>(choiceContext, enemy, ignite, Owner.Creature, this);
            if (enemy.IsAlive)
            {
                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, weak, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(IgnitePower)].UpgradeValueBy(1m);
        DynamicVars.Weak.UpgradeValueBy(1m);
    }
}
