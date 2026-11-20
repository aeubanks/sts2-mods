using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Free Function (FreeFunctionPower): The next Amount Function(s) you create cost 0 until played.
/// </summary>
public sealed class FreeFunctionPower : BronzePower, IOnFunctionCompiledListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip
    ];

    public async Task OnFunctionCompiledAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (!function.EnergyCost.CostsX)
            function.EnergyCost.SetUntilPlayed(0);

        Flash();
        await PowerCmd.Decrement(this);
    }
}
