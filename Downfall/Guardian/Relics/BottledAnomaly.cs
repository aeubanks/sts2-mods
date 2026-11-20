using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Guardian.Relics;

public sealed class BottledAnomaly : BottledGuardianRelic
{
    private CardModel? _stashedCard;
    private int _turnCounter;

    public override RelicRarity Rarity => RelicRarity.Shop;
    protected override string SelectionPromptKey => "DOWNFALL-BOTTLED_ANOMALY.selectionScreenPrompt";

    public override bool ShowCounter => _stashedCard is not null && _turnCounter > 0;
    public override int DisplayAmount => _turnCounter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => GetBottledCardHoverTips();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _stashedCard = null;
        _turnCounter = 0;
    }

    public override Task BeforeCombatStart()
    {
        _stashedCard = null;
        _turnCounter = 0;
        return base.BeforeCombatStart();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _stashedCard = null;
        _turnCounter = 0;
        SyncStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner
            || combatState.RoundNumber != 1
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.CompletedTask;
        }

        var drawCard = PileType.Draw.GetPile(Owner).Cards.FirstOrDefault(IsBottledCombatCard);
        if (drawCard is null)
            return Task.CompletedTask;

        Flash();
        drawCard.RemoveFromCurrentPile();
        _stashedCard = drawCard;
        _turnCounter = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _stashedCard is null
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.PlayerCombatState is not { } pcs)
        {
            return;
        }

        _turnCounter++;
        InvokeDisplayAmountChanged();

        if (_turnCounter < 3)
            return;

        var card = _stashedCard;
        _stashedCard = null;
        _turnCounter = 0;
        Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();

        Flash();
        if (!card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
        {
            card.EnergyCost.SetThisTurn(0, reduceOnly: true);
        }

        var dest = pcs.Hand.Cards.Count >= 10 ? PileType.Discard : PileType.Hand;
        await CardPileCmd.Add(card, dest);
    }
}
