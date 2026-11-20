using System.Text;
using BaseLib.Utils;
using Downfall.Core;
using Downfall.Guardian.Orbs;
using Downfall.Guardian.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Guardian;

[Pool(typeof(GuardianCardPool))]
public abstract class GuardianCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    private string _socketedGemsCsv = string.Empty;

    protected override string CharacterKey => TheGuardian.CharKey;

    public virtual int MaxSockets => 0;
    public virtual bool IsSelfStasis => false;
    public virtual bool IsVolatile => false;
    public virtual bool IsGem => false;
    public virtual GemType? ThisGemType => null;
    public virtual bool IsBeam => false;
    public virtual bool UsesDefensiveModeGlow => false;
    public virtual bool UsesStasisGlow => false;

    [SavedProperty]
    public string SocketedGemsCsv
    {
        get => _socketedGemsCsv;
        set
        {
            AssertMutable();
            _socketedGemsCsv = value ?? string.Empty;
        }
    }

    public IReadOnlyList<GemType> GetSocketedGems() => GuardianGems.ParseCsv(_socketedGemsCsv);

    public int OpenSockets => Math.Max(0, MaxSockets - GetSocketedGems().Count);

    public bool TrySocketGem(GemType gem)
    {
        if (OpenSockets <= 0)
            return false;

        var list = GetSocketedGems().ToList();
        list.Add(gem);
        SocketedGemsCsv = GuardianGems.ToCsv(list);
        return true;
    }

    public void CopySocketsFrom(GuardianCard source)
    {
        if (string.IsNullOrEmpty(source.SocketedGemsCsv))
            return;

        SocketedGemsCsv = source.SocketedGemsCsv;
    }

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null
        && ((UsesDefensiveModeGlow && Owner?.Creature.HasPower<DefensiveModePower>() == true)
            || (UsesStasisGlow && GuardianMechanics.GetStasisOrbs(Owner).Count > 0));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("SocketText", BuildSocketDescription());
    }

    private string BuildSocketDescription()
    {
        var gems = GetSocketedGems();
        int totalSlots = Math.Max(MaxSockets, gems.Count);
        if (totalSlots <= 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < totalSlots; i++)
        {
            sb.Append('\n');
            if (i < gems.Count)
            {
                var gem = gems[i];
                sb.Append("[gold]").Append(GuardianGems.GetDisplayName(gem)).Append(":[/gold] ");
                sb.Append(GuardianGems.GetShortEffect(gem));
            }
            else
            {
                sb.Append("[gold]Socket:[/gold] [blue](Empty)[/blue]");
            }
        }
        return sb.ToString();
    }

    protected async Task TriggerSocketedGemsAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay, int repeatCount = 1)
    {
        var gems = GetSocketedGems();
        if (gems.Count == 0 || repeatCount <= 0)
            return;

        for (int r = 0; r < repeatCount; r++)
        {
            foreach (var gem in gems)
            {
                if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return;
                await GuardianGems.TriggerGemAsync(gem, choiceContext, Owner, cardPlay);
            }
        }
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        await base.AfterCardChangedPiles(card, oldPileType, clonedBy);

        if (!IsSelfStasis
            || !ReferenceEquals(card, this)
            || oldPileType != PileType.Play
            || Pile?.Type != PileType.Discard
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await GuardianMechanics.PlaceInStasisAsync(new ThrowingPlayerChoiceContext(), Owner, this, isSelfStasis: true);
    }

    internal static IHoverTip BraceHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BRACE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-BRACE_KEYWORD.description"));

    internal static IHoverTip StasisHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-STASIS_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-STASIS_KEYWORD.description"));

    internal static IHoverTip AccelerateHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-ACCELERATE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-ACCELERATE_KEYWORD.description"));

    internal static IHoverTip VolatileHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-VOLATILE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-VOLATILE_KEYWORD.description"));

    internal static IHoverTip GemHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-GEM_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-GEM_KEYWORD.description"));

    internal static IHoverTip DefensiveModeHoverTip => HoverTipFactory.FromPower<DefensiveModePower>();
    internal static IHoverTip ModeShiftHoverTip => HoverTipFactory.FromPower<ModeShiftPower>();
}
