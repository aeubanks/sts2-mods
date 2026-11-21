using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Relics;

/// <summary>
/// Forbidden Fruit (Shop):
/// Upon pickup, add an Injury to your deck (StS 2 has no Parasite Curse) and gain 10 Max HP.
/// Start each combat with 1 Dexterity.
/// </summary>
public sealed class ForbiddenFruit : CollectorRelic
{
    public const int MaxHpGain = 10;
    public const int StartingDexterity = 1;

    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Injury>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    public override async Task AfterObtained()
    {
        if (Owner is null)
            return;

        var injury = Owner.RunState.CreateCard<Injury>(Owner);
        await CardPileCmd.Add(injury, PileType.Deck);
        await CreatureCmd.GainMaxHp(Owner.Creature, MaxHpGain);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 }
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, StartingDexterity, Owner.Creature, null);
    }
}
