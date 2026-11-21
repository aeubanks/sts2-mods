using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class ShreddedDoll : AwakenedRelic
{
    private bool _ritualGrantedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<RitualPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AwakenedCard.ConjureHoverTip,
        HoverTipFactory.FromPower<RitualPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _ritualGrantedThisCombat = false;
    }

    public override async Task AfterObtained()
    {
        if (Owner.Relics.OfType<RippedDoll>().FirstOrDefault() is { } oldDoll)
        {
            await RelicCmd.Remove(oldDoll);
        }

        _ritualGrantedThisCombat = false;
        Status = RelicStatus.Normal;
    }

    public override Task BeforeCombatStart()
    {
        _ritualGrantedThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _ritualGrantedThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await AwakenedSpellbook.ConjureAsync(Owner, combatState);
    }

    internal async Task OnSpellbookRefreshedAsync(PlayerChoiceContext choiceContext)
    {
        if (_ritualGrantedThisCombat
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _ritualGrantedThisCombat = true;
        Flash();
        await PowerCmd.Apply<RitualPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(RitualPower)].BaseValue,
            Owner.Creature,
            null);
    }
}
