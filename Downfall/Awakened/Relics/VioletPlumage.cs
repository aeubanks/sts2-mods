using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class VioletPlumage : AwakenedRelic
{
    private bool _usedThisCombat;
    private bool _drawNextTurn;

    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _usedThisCombat = false;
        _drawNextTurn = false;
    }

    private void ResetCombatState()
    {
        _usedThisCombat = false;
        _drawNextTurn = false;
        Status = RelicStatus.Normal;
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!_usedThisCombat
            && side == Owner.Creature.Side
            && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { Energy: > 0 })
        {
            _drawNextTurn = true;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || !_drawNextTurn
            || _usedThisCombat
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _drawNextTurn = false;
        _usedThisCombat = true;
        Status = RelicStatus.Disabled;
        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
