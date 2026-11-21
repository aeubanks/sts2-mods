using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Packmaster.Cards.Hermit;

public sealed class Virtue()
    : PackmasterCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "virtue";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Reduce", 1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int reduce = DynamicVars["Reduce"].IntValue;
        if (reduce <= 0)
            return;

        var debuffs = Owner.Creature.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff)
            .ToList();

        foreach (var debuff in debuffs)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (!Owner.Creature.Powers.Contains(debuff))
                continue;

            if (debuff.StackType == PowerStackType.Single || Math.Abs(debuff.Amount) <= reduce)
            {
                await PowerCmd.Remove(debuff);
            }
            else
            {
                decimal delta = debuff.Amount > 0 ? -reduce : reduce;
                await PowerCmd.ModifyAmount(choiceContext, debuff, delta, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Reduce"].UpgradeValueBy(1m);
    }
}
