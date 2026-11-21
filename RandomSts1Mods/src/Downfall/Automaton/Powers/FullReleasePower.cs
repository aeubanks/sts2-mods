using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Full Release (FullReleasePower): At the start of your turn, trigger all stored Full Release
/// Function subcards in order (unpowered/random-target semantics matching DownfallSTS).
/// Supports playing multiple Full Release Functions in the same combat by appending each Function's
/// non-FullRelease subcards to the stored list.
/// </summary>
public sealed class FullReleasePower : BronzePower
{
    private List<List<BronzeCard>> _storedFunctions = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("Effects", "Trigger your compiled Function effects.")
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip
    ];

    public void RegisterFunction(FunctionCard function)
    {
        AssertMutable();
        var subCards = function.EncodedCards
            .Where(c => c is not FullRelease)
            .Select(c => (BronzeCard)c.CreateClone())
            .ToList();

        _storedFunctions.Add(subCards);
        ((StringVar)DynamicVars["Effects"]).StringValue = BuildEffectsDescription();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _storedFunctions = [];
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || _storedFunctions.Count == 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        foreach (var subCards in _storedFunctions.ToList())
        {
            foreach (var sub in subCards)
            {
                if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return;

                Creature? resolvedTarget = null;
                if (sub.TargetType == TargetType.AnyEnemy)
                {
                    resolvedTarget = BronzeCard.ResolveTarget(player, null);
                    if (resolvedTarget is null)
                        continue;
                }

                await sub.ExecuteEffectAsync(
                    choiceContext,
                    player,
                    resolvedTarget,
                    cardPlay: null,
                    sourceCard: null,
                    isFromPower: true);
            }
        }
    }

    private string BuildEffectsDescription()
    {
        var owner = Owner?.Player;
        var lines = new List<string>();
        foreach (var subCards in _storedFunctions)
        {
            foreach (var sub in subCards)
            {
                string line = sub.GetFunctionEffectSummary(owner, null);
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }
        return lines.Count > 0 ? string.Join("\n", lines) : "Do nothing.";
    }
}
