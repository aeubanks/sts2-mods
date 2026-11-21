using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Awakened.Potions;

public sealed class PhaseSkip : AwakenedPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.CombatState is { } combatState)
        {
            await AwakenedSpellbook.TryCheckAwakeningAsync(combatState, Owner, choiceContext, force: true);
        }

        if (Owner.PlayerCombatState is { } pcs)
        {
            foreach (var card in pcs.AllCards.ToList())
            {
                if (card.IsUpgradable)
                {
                    CardCmd.Upgrade(card);
                }
            }
        }
    }
}
