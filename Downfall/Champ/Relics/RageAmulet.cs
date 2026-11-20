using Downfall.Champ.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Champ.Relics;

public sealed class RageAmulet : ChampRelic
{
    public const int VigorThreshold = 8;
    private int _vigorRemainder;
    private int _pendingStrength;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => IsMutable && Owner?.Creature.CombatState is not null;
    public override int DisplayAmount => _vigorRemainder;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _vigorRemainder = 0;
        _pendingStrength = 0;
    }

    public override Task BeforeCombatStart()
    {
        _vigorRemainder = 0;
        _pendingStrength = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _vigorRemainder = 0;
        _pendingStrength = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner
            || cardPlay.Card.Type != CardType.Attack
            || cardPlay.Card is CrookedStrike
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.CompletedTask;
        }

        int vigor = Owner.Creature.GetPowerAmount<VigorPower>();
        if (vigor <= 0)
            return Task.CompletedTask;

        int total = vigor + _vigorRemainder;
        int strGain = total / VigorThreshold;
        _vigorRemainder = total % VigorThreshold;
        InvokeDisplayAmountChanged();

        if (strGain > 0)
        {
            _pendingStrength += strGain;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_pendingStrength <= 0
            || cardPlay.Card.Owner != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int strGain = _pendingStrength;
        _pendingStrength = 0;
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, strGain, Owner.Creature, null);
    }
}
