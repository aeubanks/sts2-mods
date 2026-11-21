using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Relics;

/// <summary>
/// Thimble Helm (Rare):
/// Start each combat with 3 Temp HP. While you have Temp HP, cards grant 1 additional Block.
/// </summary>
public sealed class ThimbleHelm : CollectorRelic
{
    public const int StartingTempHp = 3;
    public const int BonusBlock = 1;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.TempHpHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 }
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await CollectorMechanics.GainTempHpAsync(choiceContext, Owner.Creature, StartingTempHp, null);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<MoreBlockWithTempHPPower>(
                choiceContext,
                Owner.Creature,
                BonusBlock,
                Owner.Creature,
                null);
        }
    }
}
