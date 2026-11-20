using BaseLib.Utils;
using Downfall.Automaton.Cards;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton;

[Pool(typeof(AutomatonCardPool))]
public abstract class BronzeCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    private bool _encodeAfterPlay;
    private PlayerChoiceContext? _pendingPlayChoiceContext;
    private int _sequenceSlotIndex = -1;
    private bool _compileEnabled = true;

    protected override string CharacterKey => TheAutomaton.CharKey;

    public static IHoverTip EncodeHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_ENCODE.title"),
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_ENCODE.description"));

    public static IHoverTip CompileHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_COMPILE.title"),
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_COMPILE.description"));

    public static IHoverTip FunctionHoverTip => HoverTipFactory.FromCard<FunctionCard>();

    public static IHoverTip InsertHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_INSERT.title"),
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_INSERT.description"));

    public static IHoverTip CycleHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_CYCLE.title"),
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_CYCLE.description"));

    public static IHoverTip BlurHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_BLUR.title"),
        new LocString("gameplay_ui", "DOWNFALL-BRONZE_BLUR.description"));

    /// <summary>
    /// Whether this card Encodes into the Function sequence when played.
    /// </summary>
    public virtual bool Encodes => false;

    /// <summary>
    /// Whether this card's Compile effect is a negative Error (suppressible by Clean Code / RemoveNextError / Debug).
    /// </summary>
    public virtual bool IsCompileError => false;

    /// <summary>
    /// Slot index inside the Function sequence (0..2), or -1 when not encoded.
    /// </summary>
    public int SequenceSlotIndex
    {
        get => _sequenceSlotIndex;
        internal set
        {
            AssertMutable();
            _sequenceSlotIndex = value;
        }
    }

    /// <summary>
    /// Whether this card's Compile hook will trigger when compiled (false when stripped by Clean Code / Debug / RemoveNextError).
    /// </summary>
    public bool CompileEnabled
    {
        get => _compileEnabled;
        internal set
        {
            AssertMutable();
            _compileEnabled = value;
        }
    }

    public bool IsFirstInSequence => _sequenceSlotIndex == 0;
    public bool IsLastInSequence => _sequenceSlotIndex == AutomatonMechanics.FunctionCapacity - 1;

    /// <summary>
    /// Function naming fragments (adjective for slot 0, noun for slot 1, optional suffix for slot 2+, optional triplicate).
    /// </summary>
    public virtual string FunctionAdjective => Title.Replace(" ", string.Empty).ToLowerInvariant();
    public virtual string FunctionNoun => Title.Replace(" ", string.Empty);
    public virtual string FunctionBonusChar => string.Empty;
    public virtual string? FunctionTriplicateName => null;
    public virtual bool ContributesFunctionName => true;

    /// <summary>
    /// Explicit list of DynamicVar names that Fine Tuning / Sentient Form (VerifyPower) can increment on this card.
    /// Damage and Block vars are handled automatically unless overridden.
    /// </summary>
    protected virtual IEnumerable<string> ExtraFineTunableVarNames => [];

    public virtual void ApplyFineTune(int amount)
    {
        if (amount <= 0)
            return;

        AssertMutable();
        if (DynamicVars.ContainsKey("Damage"))
            DynamicVars.Damage.BaseValue += amount;
        if (DynamicVars.ContainsKey("Block"))
            DynamicVars.Block.BaseValue += amount;

        foreach (string key in ExtraFineTunableVarNames)
        {
            if (DynamicVars.ContainsKey(key))
                DynamicVars[key].BaseValue += amount;
        }
    }

    /// <summary>
    /// Concise runtime line describing this card's effect when embedded inside a FunctionCard.
    /// Return empty string for cards that add no play-time text (e.g. Terminator, FullRelease).
    /// </summary>
    public virtual string GetFunctionEffectSummary(Player? owner, Creature? target) => string.Empty;

    /// <summary>
    /// Shared gameplay execution used both when played directly (sourceCard == this, isFromPower == false)
    /// and when executed inside a compiled FunctionCard (sourceCard == functionCard) or FullReleasePower (isFromPower == true).
    /// </summary>
    public virtual Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower) => Task.CompletedTask;

    /// <summary>
    /// Runs immediately when this card enters the Function sequence (e.g. Replicate, Constructor, Separator).
    /// </summary>
    public virtual Task OnEncodedAsync(PlayerChoiceContext choiceContext, Player player, int slotIndex) =>
        Task.CompletedTask;

    /// <summary>
    /// Runs right before encoded cards are embedded into a FunctionCard (e.g. ChosenStrike permanent scaling).
    /// </summary>
    public virtual void OnCompilePreEmbed(Player player) { }

    /// <summary>
    /// Runs when this card is compiled into a FunctionCard (if CompileEnabled is true).
    /// </summary>
    public virtual Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function) =>
        Task.CompletedTask;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ExecuteEffectAsync(choiceContext, Owner, cardPlay.Target, cardPlay, sourceCard: this, isFromPower: false);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Encodes
            && cardPlay.Card == this
            && cardPlay.IsLastInSeries
            && cardPlay.ResultPile == PileType.Discard)
        {
            _encodeAfterPlay = true;
            _pendingPlayChoiceContext = choiceContext;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? source)
    {
        if (card != this || !_encodeAfterPlay || oldPileType != PileType.Play)
            return;

        _encodeAfterPlay = false;
        var ctx = _pendingPlayChoiceContext ?? new BlockingPlayerChoiceContext();
        _pendingPlayChoiceContext = null;

        if (Pile?.Type == PileType.Discard
            && Owner is not null
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            await AutomatonMechanics.EncodeCardAsync(ctx, Owner, this, isFromCardPlay: true);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _encodeAfterPlay = false;
        _pendingPlayChoiceContext = null;
        _sequenceSlotIndex = -1;
        _compileEnabled = true;
    }

    /// <summary>
    /// Helper to deal single-target damage cleanly whether invoked from a card play (this or FunctionCard)
    /// or from FullReleasePower (isFromPower == true, cardPlay == null -> Unpowered damage).
    /// </summary>
    protected async Task DealDamageAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower,
        decimal baseDamage,
        int hitCount = 1)
    {
        if (owner.Creature.CombatState is not { } combatState || CombatManager.Instance.IsOverOrEnding)
            return;

        for (int i = 0; i < hitCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            var resolved = target is { IsAlive: true }
                ? target
                : owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies.Where(e => e.IsAlive).ToList());
            if (resolved is null)
                return;

            if (isFromPower || sourceCard is null)
            {
                await CreatureCmd.Damage(choiceContext, resolved, baseDamage, ValueProp.Unpowered, owner.Creature);
            }
            else
            {
                await DamageCmd.Attack(baseDamage)
                    .FromCard(sourceCard, cardPlay)
                    .Targeting(resolved)
                    .Execute(choiceContext);
            }
        }
    }

    /// <summary>
    /// Helper to deal AoE damage cleanly from card play or FullReleasePower.
    /// </summary>
    protected async Task DealAllEnemiesDamageAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower,
        decimal baseDamage,
        int hitCount = 1)
    {
        if (owner.Creature.CombatState is not { } combatState || CombatManager.Instance.IsOverOrEnding)
            return;

        for (int i = 0; i < hitCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count == 0)
                return;

            if (isFromPower || sourceCard is null)
            {
                await CreatureCmd.Damage(choiceContext, enemies, baseDamage, ValueProp.Unpowered, owner.Creature);
            }
            else
            {
                await DamageCmd.Attack(baseDamage)
                    .FromCard(sourceCard, cardPlay)
                    .TargetingAllOpponents(combatState)
                    .Execute(choiceContext);
            }
        }
    }

    /// <summary>
    /// Helper to gain Block cleanly from card play (respecting Dexterity/Frail) or FullReleasePower (Unpowered).
    /// </summary>
    protected async Task GainBlockAsync(
        Player owner,
        CardPlay? cardPlay,
        bool isFromPower,
        decimal baseBlock)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding || baseBlock <= 0)
            return;

        if (isFromPower)
        {
            await CreatureCmd.GainBlock(owner.Creature, baseBlock, ValueProp.Unpowered, null);
        }
        else
        {
            await CreatureCmd.GainBlock(owner.Creature, baseBlock, ValueProp.Move, cardPlay);
        }
    }
}

[Pool(typeof(TokenCardPool))]
public abstract class BronzeTokenCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = false)
    : BronzeCard(cost, type, rarity, target, showInCardLibrary);
