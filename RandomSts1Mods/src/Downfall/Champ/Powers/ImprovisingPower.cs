using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Champ.Powers;

public sealed class ImprovisingPower : ChampPower, IChampStanceChangedListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnStanceChangedAsync(
        PlayerChoiceContext choiceContext,
        ChampStanceKind previous,
        ChampStanceKind current)
    {
        if (current != ChampStanceKind.None
            && previous != current
            && Owner.Player is { } player
            && Owner.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await ChampStances.TriggerSkillBonusAsync(player, choiceContext, current, Amount);
        }
    }
}
