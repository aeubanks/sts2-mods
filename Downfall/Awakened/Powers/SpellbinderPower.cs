using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Awakened.Powers;

public sealed class SpellbinderPower : AwakenedPower
{
    protected override string IconKey => "spellbinder_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AwakenedCard.ConjureHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var conjured = await AwakenedSpellbook.ConjureAsync(player, CombatState);
            if (conjured is null)
                break;
        }
    }
}
