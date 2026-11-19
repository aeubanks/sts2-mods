using Downfall.Gremlins.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Gremlins.Relics;

public sealed class TagTeamwork : GremlinRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<TagTeam>()
    ];

    public override async Task AfterObtained()
    {
        int count = DynamicVars.Cards.IntValue;
        for (int i = 0; i < count; i++)
        {
            var tagTeam = Owner.RunState.CreateCard<TagTeam>(Owner);
            await CardPileCmd.Add(tagTeam, PileType.Deck);
        }
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card is not TagTeam
            || card.Owner != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, 1, Owner);
    }
}
