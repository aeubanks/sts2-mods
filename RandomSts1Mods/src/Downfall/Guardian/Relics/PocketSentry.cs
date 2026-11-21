using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Guardian.Relics;

public sealed class PocketSentry : GuardianRelic
{
    private int _turnCount;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => true;
    public override int DisplayAmount => _turnCount;

    [SavedProperty]
    public int TurnCount
    {
        get => _turnCount;
        set
        {
            AssertMutable();
            _turnCount = Math.Clamp(value, 0, 2);
            InvokeDisplayAmountChanged();
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        _turnCount++;
        if (_turnCount < 3)
        {
            InvokeDisplayAmountChanged();
            return;
        }

        _turnCount = 0;
        InvokeDisplayAmountChanged();
        Flash();

        foreach (var enemy in combatState.HittableEnemies.ToList())
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
            if (enemy.IsAlive)
            {
                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, 1m, Owner.Creature, null);
            }
        }
    }
}
