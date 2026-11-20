using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Soul Stone (SoulConsumer): Whenever you Exhaust 4 cards, Ignite the current Ghostflame.
/// </summary>
public sealed class SoulConsumer : HexaRelic
{
    public const int ExhaustThreshold = 4;

    private bool _inCombat;
    private int _exhaustCounter;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => _inCombat;
    public override int DisplayAmount => _exhaustCounter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Count", ExhaustThreshold)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HexaCard.IgniteHoverTip,
        HexaCard.GhostflameHoverTip
    ];

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _exhaustCounter = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _exhaustCounter = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _inCombat = true;
        _exhaustCounter++;
        if (_exhaustCounter < ExhaustThreshold)
        {
            InvokeDisplayAmountChanged();
            return;
        }

        _exhaustCounter = 0;
        InvokeDisplayAmountChanged();
        Flash();
        await HexaMechanics.IgniteActiveAsync(choiceContext, Owner, extinguishFirstIfIgnited: true);
    }
}
