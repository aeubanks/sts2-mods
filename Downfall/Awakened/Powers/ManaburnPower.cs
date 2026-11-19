using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Awakened.Powers;

public sealed class ManaburnPower : AwakenedPower
{
    protected override string IconKey => "manaburn_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task TriggerEnergyDrainAsync(PlayerChoiceContext choiceContext, int energyLost)
    {
        if (!Owner.IsAlive || Amount <= 0 || energyLost <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            Amount * energyLost,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card is StsCards.Void && card.Owner.Creature.Side != Owner.Side)
        {
            await TriggerEnergyDrainAsync(choiceContext, 1);
        }
    }

    internal static async Task NotifyPlayerEnergyDrainedAsync(
        ICombatState? combatState,
        PlayerChoiceContext choiceContext,
        int energyLost)
    {
        if (combatState is null || energyLost <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var enemy in combatState.HittableEnemies.ToList())
        {
            if (enemy.GetPower<ManaburnPower>() is { Amount: > 0 } manaburn)
            {
                await manaburn.TriggerEnergyDrainAsync(choiceContext, energyLost);
            }
        }
    }
}
