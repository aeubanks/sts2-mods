using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Cards.Quiet;

public sealed class Shine()
    : PackmasterCard(-1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    private bool _pendingHandDrawUpgrade;

    protected override string PortraitKey => "shine";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Retain
    ];

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!ReferenceEquals(card, this))
            return Task.CompletedTask;

        if (fromHandDraw)
            _pendingHandDrawUpgrade = true;
        else
            UpgradeHandCards();

        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (_pendingHandDrawUpgrade && player == Owner)
        {
            _pendingHandDrawUpgrade = false;
            if (Pile?.Type == PileType.Hand)
                UpgradeHandCards();
        }
        return Task.CompletedTask;
    }

    private void UpgradeHandCards()
    {
        var upgradable = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this) && c.IsUpgradable)
            .ToList();

        if (upgradable.Count == 0)
            return;

        if (IsUpgraded)
        {
            foreach (var c in upgradable)
                CardCmd.Upgrade(c);
        }
        else
        {
            var chosen = upgradable[Owner.RunState.Rng.CombatCardSelection.NextInt(upgradable.Count)];
            CardCmd.Upgrade(chosen);
        }
    }
}
