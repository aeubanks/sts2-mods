using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Allocate (Uncommon, 1 -> 0 cost):
/// Gain 1 Energy for each Status card in your Draw Pile.
/// </summary>
public sealed class Allocate() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        bool inCombat = IsMutable && CombatState is not null && Owner?.PlayerCombatState is not null;
        int statusCount = inCombat
            ? PileType.Draw.GetPile(Owner!).Cards.Count(c => c.Type == CardType.Status)
            : 0;
        description.Add("InCombat", inCombat);
        description.Add("StatusCount", statusCount);
    }

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.PlayerCombatState is null || !owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int statusCount = PileType.Draw.GetPile(owner).Cards.Count(c => c.Type == CardType.Status);
        if (statusCount > 0)
            await PlayerCmd.GainEnergy(statusCount, owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
