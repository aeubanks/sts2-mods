using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Extinguish and Ignite all Ghostflames in order (0..5), then set active slot to the last Ghostflame.
/// Exhaust. Cost: 4 (3).
/// </summary>
public sealed class UnlimitedPower() : HexaCard(4, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IgniteHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = HexaMechanics.GetGhostflames(Owner);
        if (flames.Count == 0)
            return;

        foreach (var gf in flames)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await HexaMechanics.IgniteAsync(choiceContext, Owner, gf, extinguishFirstIfIgnited: true);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            HexaCombatState.Get(combatState).ActiveSlotIndex = flames.Count - 1;
            HexaMechanics.RefreshGhostflameVisuals(Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
