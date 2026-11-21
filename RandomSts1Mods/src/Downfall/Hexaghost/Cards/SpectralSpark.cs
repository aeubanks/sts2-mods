using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Naughty Spirit (SpectralSpark): Apply 3 (5) Soulburn.
/// If the active Ghostflame is Ignited, return this card to your Hand and Retract.
/// </summary>
public sealed class SpectralSpark() : HexaCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private bool _returnToHandAfterPlay;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip,
        IgniteHoverTip,
        RetractHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        _returnToHandAfterPlay = false;

        await ApplySoulburnAsync(choiceContext, cardPlay.Target, DynamicVars["Soulburn"].IntValue);

        var active = HexaMechanics.GetActiveGhostflame(Owner);
        if (active is { IsIgnited: true } && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            _returnToHandAfterPlay = true;
            await HexaMechanics.RetractAsync(choiceContext, Owner);
        }
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? source)
    {
        if (card != this || !_returnToHandAfterPlay || oldPileType != PileType.Play)
            return;

        _returnToHandAfterPlay = false;
        if (Pile?.Type == PileType.Discard
            && Owner is not null
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && PileType.Hand.GetPile(Owner).Cards.Count < CardPile.MaxCardsInHand)
        {
            await CardPileCmd.Add(this, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(2m);
    }
}
