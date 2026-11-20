using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Instant Inferno (GhostflameInferno): Retain. Apply 12 (21) Soulburn.
/// Switch to Slot 5 and Ignite the Inferno Ghostflame. Exhaust.
/// </summary>
public sealed class GhostflameInferno() : HexaCard(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 12m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip,
        IgniteHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await ApplySoulburnAsync(choiceContext, cardPlay.Target, DynamicVars["Soulburn"].IntValue);

        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = HexaMechanics.GetGhostflames(Owner);
        if (flames.Count > 5)
        {
            HexaCombatState.Get(combatState).ActiveSlotIndex = 5;
            if (flames[5] is InfernoGhostflameOrb inferno)
                await HexaMechanics.IgniteAsync(choiceContext, Owner, inferno, extinguishFirstIfIgnited: true);
            HexaMechanics.RefreshGhostflameVisuals(Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(9m);
    }
}
