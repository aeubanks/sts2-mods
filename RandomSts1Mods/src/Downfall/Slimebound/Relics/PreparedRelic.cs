using Downfall.Slimebound.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Slimebound.Relics;

public sealed class PreparedRelic : SlimeRelic
{
    private bool _isArmed;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    [SavedProperty]
    public bool IsArmed
    {
        get => _isArmed;
        set
        {
            AssertMutable();
            _isArmed = value;
            Status = _isArmed ? RelicStatus.Active : RelicStatus.Normal;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Prepare>()
    ];

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom)
        {
            Flash();
            IsArmed = true;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 } combatState
            || !IsArmed
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        IsArmed = false;
        Flash();
        var prepare = combatState.CreateCard<Prepare>(Owner);
        prepare.EnergyCost.SetThisTurn(0, reduceOnly: true);
        await CardPileCmd.AddGeneratedCardToCombat(prepare, PileType.Hand, Owner);
    }
}
