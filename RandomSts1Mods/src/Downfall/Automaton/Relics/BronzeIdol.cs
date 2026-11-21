using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Downfall.Automaton.Relics;

/// <summary>
/// Bronze Idol (Shop): Status cards may now be played for useful effects.
/// Uses native StS 2 AfterCardGeneratedForCombat + AfterCardDrawn + CardCmd.Transform
/// (in-place replacement preserving pile position, upgrades, and non-standard Ethereal/Exhaust keywords).
/// </summary>
public sealed class BronzeIdol : BronzeRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<BronzeDaze>(),
        HoverTipFactory.FromCard<BronzeGrievousWound>(),
        HoverTipFactory.FromCard<BronzeIgnite>(),
        HoverTipFactory.FromCard<BronzeIntoTheVoid>(),
        HoverTipFactory.FromCard<BronzeUsefulSlime>()
    ];

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        await TryReplaceStatusAsync(card);
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        await TryReplaceStatusAsync(card);
    }

    private async Task TryReplaceStatusAsync(CardModel card)
    {
        if (card.Owner != Owner
            || card.Type != CardType.Status
            || card is BronzeGoodStatusCard
            || card.Pile is not { IsCombatPile: true }
            || Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var replacement = CreateGoodStatus(combatState, Owner, card);
        Flash();
        await CardCmd.Transform(card, replacement, CardPreviewStyle.None);
    }

    internal static BronzeGoodStatusCard CreateGoodStatus(ICombatState combatState, Player owner, CardModel original)
    {
        BronzeGoodStatusCard replacement = original switch
        {
            Dazed => (BronzeGoodStatusCard)combatState.CreateCard<BronzeDaze>(owner),
            Wound => (BronzeGoodStatusCard)combatState.CreateCard<BronzeGrievousWound>(owner),
            Burn => (BronzeGoodStatusCard)combatState.CreateCard<BronzeIgnite>(owner),
            MegaCrit.Sts2.Core.Models.Cards.Void => (BronzeGoodStatusCard)combatState.CreateCard<BronzeIntoTheVoid>(owner),
            Slimed => (BronzeGoodStatusCard)combatState.CreateCard<BronzeUsefulSlime>(owner),
            _ => CreateUnknownStatus(combatState, owner, original)
        };

        if (original.IsUpgraded && replacement.IsUpgradable)
            CardCmd.Upgrade(replacement);

        return replacement;
    }

    private static BronzeUnknownStatus CreateUnknownStatus(ICombatState combatState, Player owner, CardModel original)
    {
        var unknown = (BronzeUnknownStatus)combatState.CreateCard<BronzeUnknownStatus>(owner);
        if (original.Keywords.Contains(CardKeyword.Ethereal))
            unknown.AddKeyword(CardKeyword.Ethereal);
        if (original.Keywords.Contains(CardKeyword.Exhaust))
            unknown.AddKeyword(CardKeyword.Exhaust);
        return unknown;
    }
}
