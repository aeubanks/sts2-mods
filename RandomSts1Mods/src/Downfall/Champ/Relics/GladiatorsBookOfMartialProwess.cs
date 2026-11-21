using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Champ.Relics;

public sealed class GladiatorsBookOfMartialProwess : ChampRelic, IChampStanceChangedListener
{
    private bool _enteredStance;
    private bool _playedCombo;
    private bool _playedFinisher;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.ComboHoverTip,
        ChampCard.FinisherHoverTip,
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        ResetFlags();
    }

    private void ResetFlags()
    {
        _enteredStance = false;
        _playedCombo = false;
        _playedFinisher = false;
    }

    public override Task BeforeCombatStart()
    {
        ResetFlags();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetFlags();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Creature.Side)
        {
            ResetFlags();
        }
        return Task.CompletedTask;
    }

    public async Task OnStanceChangedAsync(
        PlayerChoiceContext choiceContext,
        ChampStanceKind previous,
        ChampStanceKind current)
    {
        if (current != ChampStanceKind.None && previous != current)
        {
            _enteredStance = true;
            await TryTriggerAsync(choiceContext);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card is not ChampCard champCard)
            return;

        if (champCard.IsCombo)
            _playedCombo = true;
        if (champCard.IsFinisher)
            _playedFinisher = true;

        await TryTriggerAsync(choiceContext);
    }

    private async Task TryTriggerAsync(PlayerChoiceContext choiceContext)
    {
        if (!_enteredStance
            || !_playedCombo
            || !_playedFinisher
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        ResetFlags();
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        }
    }
}
