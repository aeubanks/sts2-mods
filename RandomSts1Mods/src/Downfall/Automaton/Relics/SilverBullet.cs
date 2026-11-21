using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Automaton.Relics;

/// <summary>
/// Silver Bullet (Common): At the start of each combat, add a copy of Piercing Shot to your Hand.
/// It costs 0 until played.
/// </summary>
public sealed class SilverBullet : BronzeRelic
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<WhirlingStrike>()
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeredThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _triggeredThisCombat
            || player.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Flash();
        var shot = combatState.CreateCard<WhirlingStrike>(Owner);
        shot.EnergyCost.SetUntilPlayed(0);
        await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(shot, Owner);
    }
}
