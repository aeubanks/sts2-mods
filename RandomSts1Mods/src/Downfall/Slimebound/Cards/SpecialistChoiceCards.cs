using BaseLib.Utils;
using Downfall.Slimebound.Orbs;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Slimebound.Cards;

[Pool(typeof(TokenCardPool))]
public abstract class SpecialistChoiceCard() : SlimeCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string PortraitKey => "split_specialist";
    public override bool CanBeGeneratedInCombat => false;

    public abstract Task SpawnChoiceAsync(PlayerChoiceContext choiceContext);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        SpawnChoiceAsync(choiceContext);
}

public sealed class ChooseBronzeSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<BronzeSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<BronzeSlime>(choiceContext, Owner);
}

public sealed class ChooseHexSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<HexSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<HexSlime>(choiceContext, Owner);
}

public sealed class ChooseTorchHeadSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<TorchHeadSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<TorchHeadSlime>(choiceContext, Owner);
}

public sealed class ChooseCultistSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<CultistSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<CultistSlime>(choiceContext, Owner);
}

public sealed class ChooseProtectorSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<ProtectorSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<ProtectorSlime>(choiceContext, Owner);
}

public sealed class ChooseChampSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<ChampSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<ChampSlime>(choiceContext, Owner);
}

public sealed class ChooseDrawingSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<DrawingSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<DrawingSlime>(choiceContext, Owner);
}

public sealed class ChooseSlowingSlime : SpecialistChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromOrb<SlowingSlime>()];
    public override Task SpawnChoiceAsync(PlayerChoiceContext choiceContext) => SlimeMechanics.SplitSlimeAsync<SlowingSlime>(choiceContext, Owner);
}
