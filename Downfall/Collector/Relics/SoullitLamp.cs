using Downfall.Collector.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Relics;

/// <summary>
/// Soul-lit Lamp (SoullitLamp, Uncommon):
/// Upon pickup, gain 3 Essence. At the start of combat, add an Ember into your hand.
/// </summary>
public sealed class SoullitLamp : CollectorRelic
{
    public const int EssenceGain = 3;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Ember>()
    ];

    public override Task AfterObtained()
    {
        if (Owner is not null)
        {
            CollectorRunState.GetOrCreate(Owner).ChangeEssence(Owner, EssenceGain);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        var ember = combatState.CreateCard<Ember>(player);
        await CollectorMechanics.AddGeneratedCardToHandOrDiscardAsync(ember, player);
    }
}
