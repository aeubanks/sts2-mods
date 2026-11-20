using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Relics;

/// <summary>
/// Frost Primer (Timepiece, Rare): All Functions you create gain Retain.
/// </summary>
public sealed class Timepiece : BronzeRelic, IOnFunctionCompiledListener
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    public Task OnFunctionCompiledAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player != Owner || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return Task.CompletedTask;

        if (!function.Keywords.Contains(CardKeyword.Retain))
            function.AddKeyword(CardKeyword.Retain);

        Flash();
        return Task.CompletedTask;
    }
}
