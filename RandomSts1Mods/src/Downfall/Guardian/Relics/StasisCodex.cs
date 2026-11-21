using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Guardian.Relics;

public sealed class StasisCodex : GuardianRelic
{
    private const int MaxTurns = 3;
    private int _turnsUsed;

    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool ShowCounter => CombatManager.Instance.IsInProgress && _turnsUsed < MaxTurns;
    public override int DisplayAmount => Math.Max(0, MaxTurns - _turnsUsed);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.StasisHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _turnsUsed = 0;
    }

    public override Task BeforeCombatStart()
    {
        _turnsUsed = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _turnsUsed = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _turnsUsed >= MaxTurns
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _turnsUsed++;
        if (_turnsUsed >= MaxTurns)
            Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();

        if (!GuardianMechanics.CanPlaceInStasis(Owner))
            return;

        Flash();
        await GuardianMechanics.ChooseAndPlaceRandomCardInStasisAsync(choiceContext, Owner, choiceCount: 3, canSkip: true);
    }
}
