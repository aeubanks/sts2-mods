using Downfall.Champ.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Powers;

public sealed class CounterPower : ChampPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Riposte>()
    ];

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || dealer is null
            || dealer == Owner
            || !props.IsPoweredAttack()
            || Amount <= 0
            || Owner.Player is not { } player
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int counterAmount = Amount;
        int riposteCount = 1;
        Flash();
        await PowerCmd.Remove(this);

        if (Owner.GetPower<ParryPower>() is { } parry)
        {
            riposteCount += parry.Amount;
            await parry.ConsumeAsync();
        }

        if (Owner.GetPower<GladiatorFormPower>() is { } gladForm)
        {
            await gladForm.OnCounterSpentAsync(choiceContext, counterAmount);
        }

        for (int i = 0; i < riposteCount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var riposte = combatState.CreateCard<Riposte>(player);
            riposte.DynamicVars.Damage.BaseValue = counterAmount;
            await CardPileCmd.AddGeneratedCardToCombat(riposte, PileType.Hand, player);
        }
    }
}
