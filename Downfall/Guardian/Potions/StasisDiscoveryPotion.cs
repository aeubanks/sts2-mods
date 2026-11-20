using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Guardian.Potions;

public sealed class StasisDiscoveryPotion : GuardianPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Count", 3m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.StasisHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        int count = DynamicVars["Count"].IntValue;
        int currentSlots = Owner.PlayerCombatState?.OrbQueue.Capacity ?? 0;
        if (currentSlots < count)
        {
            await OrbCmd.AddSlots(Owner, count - currentSlots);
        }

        for (int i = 0; i < count; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding || !GuardianMechanics.CanPlaceInStasis(Owner))
                break;

            await GuardianMechanics.ChooseAndPlaceRandomCardInStasisAsync(choiceContext, Owner, choiceCount: 3, canSkip: true);
        }
    }
}
