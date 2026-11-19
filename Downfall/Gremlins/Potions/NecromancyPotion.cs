using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Potions;

public sealed class NecromancyPotion : GremlinPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", 20m)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive)
            return;

        int healAmt = Math.Max(1, (int)(Owner.Creature.MaxHp * DynamicVars["HealPercent"].BaseValue / 100m));
        await CreatureCmd.Heal(Owner.Creature, healAmt);
    }
}
