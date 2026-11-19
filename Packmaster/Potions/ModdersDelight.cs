using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Packmaster.Packs;

namespace Packmaster.Potions;

public sealed class ModdersDelight : PackmasterPotion
{
    protected override string IconKey => "modders_delight";

    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        var player = target!.Player!;

        if (PileType.Discard.GetPile(player).Cards.Count > 0)
        {
            await CardPileCmd.Shuffle(choiceContext, player);
        }

        await PackHelper.FetchDistinctFromDrawAsync(player, DynamicVars.Cards.IntValue, PackRegistry.GetForCard);
    }
}
