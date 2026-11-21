using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;
using Packmaster.Packs;

namespace Packmaster.Character;

public class PackmasterCardPool : CustomCardPoolModel
{
    public override string Title => ThePackmaster.CharacterId;

    public override string BigEnergyIconPath => "res://Packmaster/images/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Packmaster/images/charui/text_energy.png";

    public override Color ShaderColor => ThePackmaster.CharacterColor;
    public override Color DeckEntryCardColor => ThePackmaster.CharacterColor;

    public override bool IsColorless => false;

    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards)
    {
        var baseFiltered = base.FilterThroughEpochs(unlockState, cards);
        var activePacks = PackRunState.GetActivePackIdsForCurrentRun();
        if (activePacks is null)
            return baseFiltered;

        return baseFiltered.Where(card =>
            PackRegistry.GetForCard(card) is { } pack
                ? activePacks.Contains(pack.Id)
                : card.Rarity is not (CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
        ).ToList();
    }
}
