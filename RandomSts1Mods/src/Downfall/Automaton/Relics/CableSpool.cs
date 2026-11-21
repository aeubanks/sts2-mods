using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Automaton.Relics;

/// <summary>
/// Cable Spool (Uncommon): Encode a copy of the first Encodable card you play each combat.
/// </summary>
public sealed class CableSpool : BronzeRelic, IEncodeCopyProvider
{
    private bool _usedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.EncodeHoverTip
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _usedThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        Status = RelicStatus.Active;
        return Task.CompletedTask;
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            _usedThisCombat = false;
            Status = RelicStatus.Active;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public Task<int> ConsumeExtraEncodeCopiesAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        BronzeCard card,
        bool isFromCardPlay)
    {
        if (player != Owner
            || !isFromCardPlay
            || _usedThisCombat
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.FromResult(0);
        }

        _usedThisCombat = true;
        Status = RelicStatus.Normal;
        Flash();
        return Task.FromResult(1);
    }
}
