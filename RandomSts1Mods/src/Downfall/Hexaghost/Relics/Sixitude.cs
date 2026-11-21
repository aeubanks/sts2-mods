using Downfall.Core;
using Downfall.Hexaghost.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Six-Point Brand (Sixitude): Whenever you play 6 non-0-cost cards,
/// gain an Echo of a basic Strike. It costs 0.
/// </summary>
public sealed class Sixitude : HexaRelic
{
    public const int CardThreshold = 6;

    private int _counter;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _counter;

    [SavedProperty]
    public int CardCounter
    {
        get => _counter;
        set
        {
            AssertMutable();
            _counter = Math.Max(0, value);
            InvokeDisplayAmountChanged();
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Count", CardThreshold)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DownfallEcho.EchoHoverTip,
        HoverTipFactory.FromCard<HexaStrike>()
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner
            || Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (cardPlay.IsAutoPlay || cardPlay.Resources.EnergySpent <= 0)
            return;

        CardCounter++;
        if (CardCounter < CardThreshold)
            return;

        CardCounter = 0;
        Flash();

        CardModel basicStrikeCanonical = Owner.Character.CardPool.AllCards
            .FirstOrDefault(c => c.Rarity == CardRarity.Basic && c.Tags.Contains(CardTag.Strike))
            ?? ModelDb.Card<HexaStrike>();

        var baseStrike = combatState.CreateCard(basicStrikeCanonical, Owner);
        var echo = DownfallEcho.MakeEcho(baseStrike);
        if (echo is not null)
        {
            echo.EnergyCost.SetThisCombat(0);
            await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, Owner);
        }
    }
}
