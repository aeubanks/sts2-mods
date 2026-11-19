using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Powers;

public sealed class ChosensVersePower : AwakenedPower
{
    private sealed class Entry(int remaining, decimal block, CardPlay? ignoredPlay)
    {
        public int Remaining { get; set; } = remaining;
        public decimal Block { get; } = block;
        public CardPlay? IgnoredPlay { get; set; } = ignoredPlay;
    }

    private List<Entry>? _entries;
    private List<Entry> Entries => _entries ??= [];

    protected override string IconKey => "chosens_verse_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Unpowered)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _entries = null;
    }

    internal void RecordEntry(int triggers, decimal blockAmount, CardPlay? sourcePlay)
    {
        Entries.Add(new Entry(triggers, blockAmount, sourcePlay));
        SyncDisplayedBlock();
    }

    private void SyncDisplayedBlock()
    {
        DynamicVars.Block.BaseValue = _entries is { Count: > 0 } ? _entries[^1].Block : 4m;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type == CardType.Attack
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (_entries is not { Count: > 0 })
        {
            if (Amount <= 0)
                return;

            Flash();
            await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            await CreatureCmd.GainBlock(Owner, DynamicVars.Block.BaseValue, ValueProp.Unpowered, null);
            await PowerCmd.Decrement(this);
            return;
        }

        int triggersFired = 0;
        decimal totalBlock = 0m;
        foreach (var entry in _entries)
        {
            if (ReferenceEquals(entry.IgnoredPlay, cardPlay))
            {
                entry.IgnoredPlay = null;
                continue;
            }

            if (entry.Remaining <= 0)
                continue;

            entry.Remaining--;
            triggersFired++;
            totalBlock += entry.Block;
        }

        _entries.RemoveAll(e => e.Remaining <= 0);
        SyncDisplayedBlock();

        if (triggersFired <= 0)
            return;

        Flash();
        await CardPileCmd.Draw(choiceContext, triggersFired, Owner.Player);
        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CreatureCmd.GainBlock(Owner, totalBlock, ValueProp.Unpowered, null);
        await PowerCmd.ModifyAmount(choiceContext, this, -triggersFired, null, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner) && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            _entries?.Clear();
            await PowerCmd.Remove(this);
        }
    }
}
