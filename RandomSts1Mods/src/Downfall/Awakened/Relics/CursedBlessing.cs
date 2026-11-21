using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class CursedBlessing : AwakenedRelic
{
    private const int RequiredChants = 3;

    private int _chantsThisCombat;
    private bool _inCombat;
    private bool _triggered;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool ShowCounter => _inCombat && !_triggered;
    public override int DisplayAmount => _chantsThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Chants", RequiredChants),
        new PowerVar<RitualPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AwakenedCard.ChantHoverTip,
        HoverTipFactory.FromPower<RitualPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _chantsThisCombat = 0;
        _inCombat = false;
        _triggered = false;
    }

    private void ResetCombatState(bool inCombat)
    {
        _inCombat = inCombat;
        _chantsThisCombat = 0;
        _triggered = false;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            ResetCombatState(true);
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState(true);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState(false);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);

        if (_triggered
            || cardPlay.Card.Owner != Owner
            || cardPlay.Card is not AwakenedCard awakenedCard
            || !awakenedCard.DidTriggerChantOnPlay(cardPlay)
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _chantsThisCombat++;

        if (_chantsThisCombat >= RequiredChants)
        {
            _triggered = true;
            Status = RelicStatus.Disabled;
            InvokeDisplayAmountChanged();
            Flash();
            await PowerCmd.Apply<RitualPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(RitualPower)].BaseValue,
                Owner.Creature,
                null);
        }
        else
        {
            InvokeDisplayAmountChanged();
        }
    }
}
