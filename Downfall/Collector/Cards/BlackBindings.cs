using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards;

/// <summary>
/// Black Bindings (Uncommon, 1 cost, Skill):
/// Apply 2 Weak, then apply 2 (4) Doom for each unique debuff on the enemy.
/// </summary>
public sealed class BlackBindings() : CollectorCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(2m),
        new IntVar("DoomPerDebuff", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        DoomHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, this);

        if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int debuffCount = target.Powers.Count(p => p.Type == PowerType.Debuff);
        if (debuffCount > 0)
        {
            decimal totalDoom = debuffCount * DynamicVars["DoomPerDebuff"].BaseValue;
            await CollectorMechanics.ApplyDoomAsync(choiceContext, target, totalDoom, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DoomPerDebuff"].UpgradeValueBy(2m);
    }
}
