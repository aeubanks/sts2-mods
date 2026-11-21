using Downfall.Guardian.Cards;
using Downfall.Guardian.Powers;
using Downfall.Guardian.RestSite;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Guardian.Relics;

public abstract class ModeShifterBase : GuardianRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<GearUp>(),
        GuardianCard.ModeShiftHoverTip
    ];

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner)
            return false;

        if (options.OfType<SocketGemRestSiteOption>().Any())
            return false;

        options.Add(new SocketGemRestSiteOption(player));
        return true;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Creature.Side
            || combatState.RoundNumber != 1
            || !participants.Contains(Owner.Creature)
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (!Owner.Creature.HasPower<ModeShiftPower>())
        {
            await PowerCmd.Apply<ModeShiftPower>(choiceContext, Owner.Creature, ModeShiftPower.StartingThreshold, Owner.Creature, null);
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        var gearUp = combatState.CreateCard<GearUp>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(gearUp, PileType.Hand, Owner);
    }
}

public sealed class ModeShifter : ModeShifterBase
{
    private bool _starterGemGranted;

    [SavedProperty]
    public bool StarterGemGranted
    {
        get => _starterGemGranted;
        set
        {
            AssertMutable();
            _starterGemGranted = value;
        }
    }

    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<ModeShifterPlus>();

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (!_starterGemGranted && Owner is not null && Owner.RunState.TotalFloor <= 1)
        {
            StarterGemGranted = true;
            var gem = GuardianGems.CreateRandomGemCardForDeck(Owner, onlyCommon: true, Owner.RunState.Rng.UpFront);
            await CardPileCmd.Add(gem, PileType.Deck);
        }
    }
}

public sealed class ModeShifterPlus : ModeShifterBase, IModeShiftTriggerListener
{
    public async Task OnModeShiftTriggeredAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        if (Owner.Creature.CombatState?.CurrentSide == CombatSide.Player)
        {
            await PlayerCmd.GainEnergy(1m, Owner);
        }
        else
        {
            await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, 2m, Owner);
        }
    }
}
