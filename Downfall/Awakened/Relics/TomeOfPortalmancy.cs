using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Awakened.Relics;

public sealed class TomeOfPortalmancy : AwakenedRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ManaburnPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<StsCards.Void>(),
        HoverTipFactory.FromPower<ManaburnPower>()
    ];

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is not StsCards.Void
            || creator != Owner
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        var ctx = new ThrowingPlayerChoiceContext();
        decimal manaburn = DynamicVars[nameof(ManaburnPower)].BaseValue;
        foreach (var enemy in combatState.HittableEnemies.ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;

            await PowerCmd.Apply<ManaburnPower>(ctx, enemy, manaburn, Owner.Creature, null);
        }
    }
}
