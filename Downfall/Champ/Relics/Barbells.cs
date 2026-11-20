using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Champ.Relics;

public sealed class Barbells : ChampRelic
{
    public const int RequiredUpgradableCount = 10;

    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool ShowCounter => true;

    public override int DisplayAmount =>
        IsMutable && Owner is not null
            ? Owner.Deck.Cards.Count(c => c.IsUpgradable)
            : 0;

    public override Task AfterObtained()
    {
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (card.Owner == Owner && (oldPileType == PileType.Deck || card.Pile?.Type == PileType.Deck))
        {
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        InvokeDisplayAmountChanged();
        if (room is not RestSiteRoom)
            return Task.CompletedTask;

        var upgradables = Owner.Deck.Cards.Where(c => c.IsUpgradable).ToList();
        if (upgradables.Count >= RequiredUpgradableCount)
        {
            var picked = Owner.RunState.Rng.Niche.NextItem(upgradables);
            if (picked is not null)
            {
                Flash();
                CardCmd.Upgrade(picked);
            }
        }

        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
