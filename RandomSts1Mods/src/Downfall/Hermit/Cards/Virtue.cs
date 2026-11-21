using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class Virtue()
    : HermitCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "virtue";

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ReduceAmount", 1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int amount = DynamicVars["ReduceAmount"].IntValue;
        if (amount <= 0)
            return;

        foreach (var power in Owner.Creature.Powers.ToList())
        {
            bool isDebuff = power.Type == PowerType.Debuff || (power.AllowNegative && power.Amount < 0);
            if (!isDebuff)
                continue;

            if (power.StackType == PowerStackType.Single)
            {
                await PowerCmd.Remove(power);
            }
            else if (power.Amount > 0)
            {
                if (power.Amount <= amount)
                    await PowerCmd.Remove(power);
                else
                    await PowerCmd.ModifyAmount(choiceContext, power, -amount, Owner.Creature, this);
            }
            else if (power.Amount < 0)
            {
                if (-power.Amount <= amount)
                    await PowerCmd.Remove(power);
                else
                    await PowerCmd.ModifyAmount(choiceContext, power, amount, Owner.Creature, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ReduceAmount"].UpgradeValueBy(1m);
    }
}
