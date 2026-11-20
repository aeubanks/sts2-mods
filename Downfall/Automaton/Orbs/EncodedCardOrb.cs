using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Orbs;

/// <summary>
/// Represents a card currently Encoded in The Automaton's 3-slot Function sequence.
/// Displays a single preloaded bronze sequence node icon with the slot number (1..3)
/// and shows the full encoded card on hover.
/// </summary>
public sealed class EncodedCardOrb : DownfallOrb
{
    private BronzeCard? _encodedCard;
    private int _sequenceSlotIndex;

    protected override string CharacterKey => TheAutomaton.CharKey;
    public override Color DarkenedColor => new("#7A6A42");
    public override bool ShowSecondaryLabel => false;
    public override string EvokePreviewText => string.Empty;

    public BronzeCard? EncodedCard => _encodedCard;

    public int SequenceSlotIndex
    {
        get => _sequenceSlotIndex;
        internal set
        {
            AssertMutable();
            _sequenceSlotIndex = Math.Max(0, value);
            if (_encodedCard is not null)
                _encodedCard.SequenceSlotIndex = _sequenceSlotIndex;
        }
    }

    public override decimal PassiveVal => _sequenceSlotIndex + 1;
    public override decimal EvokeVal => _sequenceSlotIndex + 1;
    public override string PrimaryLabelText => (_sequenceSlotIndex + 1).ToString();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        _encodedCard is not null
            ? [HoverTipFactory.FromCard(_encodedCard)]
            : [];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _encodedCard = null;
        _sequenceSlotIndex = 0;
    }

    internal void InitializeEncodedCard(BronzeCard card, int slotIndex)
    {
        AssertMutable();
        _encodedCard = card;
        _sequenceSlotIndex = Math.Max(0, slotIndex);
        card.SequenceSlotIndex = _sequenceSlotIndex;
    }

    public override Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        // Encoded cards are consumed by CompileFunctionAsync (or explicitly extracted by BitShift/ByteShift/Decompile).
        return Task.FromResult<IEnumerable<Creature>>([]);
    }
}
