using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Gremlins.Cards;

public abstract class GremlinChoiceCard(GremlinKind kind)
    : GremlinCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    public GremlinKind Kind { get; } = kind;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade()
    {
    }
}

public sealed class MadGremlinChoice() : GremlinChoiceCard(GremlinKind.Mad);
public sealed class FatGremlinChoice() : GremlinChoiceCard(GremlinKind.Fat);
public sealed class ShieldGremlinChoice() : GremlinChoiceCard(GremlinKind.Shield);
public sealed class SneakyGremlinChoice() : GremlinChoiceCard(GremlinKind.Sneaky);
public sealed class GremlinWizardChoice() : GremlinChoiceCard(GremlinKind.Wizard);
