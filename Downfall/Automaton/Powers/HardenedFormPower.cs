using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Summon Orb (HardenedFormPower): Whenever you create or play a Function,
/// gain Amount Block and deal Amount damage to a random enemy.
/// </summary>
public sealed class HardenedFormPower : BronzePower, IOnFunctionCompiledListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public async Task OnFunctionCompiledAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player != Owner.Player)
            return;

        await TriggerOrbAsync(choiceContext, player);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is not FunctionCard || cardPlay.Card.Owner != Owner.Player)
            return;

        await TriggerOrbAsync(choiceContext, cardPlay.Card.Owner);
    }

    private async Task TriggerOrbAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = BronzeCard.ResolveTarget(player, null);
        if (target is null)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
