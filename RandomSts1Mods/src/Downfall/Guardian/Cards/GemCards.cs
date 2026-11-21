using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Cards;

public abstract class GuardianGemCard(CardRarity rarity, GemType gemType)
    : GuardianCard(0, CardType.Skill, rarity, TargetType.Self)
{
    public override bool IsGem => true;
    public override GemType? ThisGemType => gemType;
    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [GemHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GuardianGems.TriggerGemAsync(gemType, choiceContext, Owner, cardPlay);
    }
}

public sealed class GemRed() : GuardianGemCard(CardRarity.Common, GemType.Red)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromPower<StrengthPower>()
    ];
}

public sealed class GemGreen() : GuardianGemCard(CardRarity.Uncommon, GemType.Green)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromPower<DexterityPower>()
    ];
}

public sealed class GemLightblue() : GuardianGemCard(CardRarity.Common, GemType.Lightblue)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromPower<ThornsPower>()
    ];
}

public sealed class GemBlue() : GuardianGemCard(CardRarity.Common, GemType.Blue)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        BraceHoverTip
    ];
}

public sealed class GemFragmented() : GuardianGemCard(CardRarity.Common, GemType.Fragmented)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromCard<CrystalShiv>()
    ];
}

public sealed class GemCyan() : GuardianGemCard(CardRarity.Uncommon, GemType.Cyan)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromCard<CrystalWard>()
    ];
}

public sealed class GemCrimson() : GuardianGemCard(CardRarity.Uncommon, GemType.Crimson)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromPower<VulnerablePower>()
    ];
}

public sealed class GemWhite() : GuardianGemCard(CardRarity.Uncommon, GemType.White);

public sealed class GemYellow() : GuardianGemCard(CardRarity.Uncommon, GemType.Yellow)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        AccelerateHoverTip
    ];
}

public sealed class GemPurple() : GuardianGemCard(CardRarity.Uncommon, GemType.Purple)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.FromPower<StrengthPower>()
    ];
}

public sealed class GemOrange() : GuardianGemCard(CardRarity.Rare, GemType.Orange)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip,
        HoverTipFactory.ForEnergy(this)
    ];
}

public sealed class GemSynthetic() : GuardianGemCard(CardRarity.Rare, GemType.Synthetic);
