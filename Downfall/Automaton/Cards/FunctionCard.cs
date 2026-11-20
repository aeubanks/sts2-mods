using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Compiled Function card produced from 1..3 Encoded cards.
/// Dynamically computes Title, Type (Power if FullRelease, Attack if any Attack, else Skill),
/// TargetType (Self if FullRelease, AnyEnemy if any single-target, AllEnemies if any AoE, else Self),
/// GainsBlock, and live Body description in AddExtraArgsToDescription.
/// </summary>
public sealed class FunctionCard() : BronzeTokenCard(1, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    private List<BronzeCard> _encodedCards = [];
    private string _compiledTitle = string.Empty;
    private bool _isFullRelease;
    private bool _playTwice;

    public IReadOnlyList<BronzeCard> EncodedCards => _encodedCards;
    public bool IsFullRelease => _isFullRelease;
    public bool PlayTwice => _playTwice;

    public override int MaxUpgradeLevel => 0;

    public override string Title => string.IsNullOrEmpty(_compiledTitle) ? base.Title : _compiledTitle;

    public override CardType Type
    {
        get
        {
            if (_isFullRelease)
                return CardType.Power;
            return _encodedCards.Any(c => c.Type == CardType.Attack)
                ? CardType.Attack
                : CardType.Skill;
        }
    }

    public override TargetType TargetType
    {
        get
        {
            if (_isFullRelease)
                return TargetType.Self;
            if (_encodedCards.Any(c => c.TargetType == TargetType.AnyEnemy))
                return TargetType.AnyEnemy;
            if (_encodedCards.Any(c => c.TargetType == TargetType.AllEnemies))
                return TargetType.AllEnemies;
            return TargetType.Self;
        }
    }

    public override bool GainsBlock => !_isFullRelease && _encodedCards.Any(c => c.GainsBlock);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("Body", "Compiled from Encoded cards.")
    ];

    internal void InitializeFromEncodedCards(IReadOnlyList<BronzeCard> encodedCards)
    {
        AssertMutable();
        _encodedCards = encodedCards.Select(c => (BronzeCard)c.CreateClone()).ToList();
        _isFullRelease = false;
        _playTwice = false;
        _compiledTitle = BuildCompiledTitle(_encodedCards);
    }

    internal void SetFullRelease()
    {
        AssertMutable();
        _isFullRelease = true;
    }

    internal void SetPlayTwice()
    {
        AssertMutable();
        _playTwice = true;
    }

    public override void ApplyFineTune(int amount)
    {
        if (amount <= 0)
            return;

        AssertMutable();
        foreach (var sub in _encodedCards)
        {
            sub.ApplyFineTune(amount);
        }
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card == this && _playTwice && !_isFullRelease)
            return playCount + 1;
        return playCount;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Body", BuildBodyText());
    }

    public string BuildBodyText()
    {
        if (_encodedCards.Count == 0)
            return "Compiled from Encoded cards.";

        var owner = IsMutable ? Owner : null;
        var lines = new List<string>(_encodedCards.Count + 2);
        if (_isFullRelease)
            lines.Add("At the start of your turn:");

        foreach (var sub in _encodedCards)
        {
            string line = sub.GetFunctionEffectSummary(owner, CurrentTarget);
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        if (_playTwice && !_isFullRelease)
            lines.Add("Play this again.");

        return lines.Count > 0 ? string.Join("\n", lines) : "Do nothing.";
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (_isFullRelease)
        {
            // FullReleasePower is wired directly here in Phase 9D.
            return;
        }

        await ExecuteSubCardsAsync(choiceContext, Owner, cardPlay.Target, cardPlay, sourceCard: this, isFromPower: false);
    }

    internal async Task ExecuteSubCardsAsync(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Players.Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.Creature.CombatState is not { } combatState)
            return;

        foreach (var sub in _encodedCards)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            Creature? resolvedTarget = null;
            if (sub.TargetType == TargetType.AnyEnemy)
            {
                resolvedTarget = target is { IsAlive: true }
                    ? target
                    : owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies.Where(e => e.IsAlive).ToList());
                if (resolvedTarget is null)
                    continue;
            }

            await sub.ExecuteEffectAsync(choiceContext, owner, resolvedTarget, cardPlay, sourceCard, isFromPower);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _encodedCards = _encodedCards.Select(c => (BronzeCard)c.CreateClone()).ToList();
    }

    private static string BuildCompiledTitle(IReadOnlyList<BronzeCard> cards)
    {
        if (cards.Count == 0)
            return "function()";

        // Exact DownfallSTS Perfection() easter egg: Constructor + Separator + Terminator
        if (cards.Count == 3
            && cards[0].GetType().Name == "Constructor"
            && cards[1].GetType().Name == "Separator"
            && cards[2].GetType().Name == "Terminator")
        {
            return "Perfection()";
        }

        // Triplicate check strictly guarded to 3 cards so 1-card Batch is never named a triplicate.
        if (cards.Count == AutomatonMechanics.FunctionCapacity
            && cards.All(c => c.GetType() == cards[0].GetType())
            && !string.IsNullOrEmpty(cards[0].FunctionTriplicateName))
        {
            return cards[0].FunctionTriplicateName!;
        }

        var named = cards.Where(c => c.ContributesFunctionName).ToList();
        if (named.Count == 0)
            return "function()";

        if (named.Count == 1)
            return $"{named[0].FunctionNoun}()";

        string prefix = named[0].FunctionAdjective + named[1].FunctionNoun;
        for (int i = 2; i < named.Count; i++)
            prefix += named[i].FunctionBonusChar;

        return $"{prefix}()";
    }
}
