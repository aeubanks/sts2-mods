using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Bright Ritual (SpectralAdrenaline): Reset the Wheel to Slot 0 and Extinguish all Ignited Ghostflames.
/// Gain 1 Energy and draw 1 card for each Ghostflame Extinguished. Exhaust. Cost: 2 (1).
/// </summary>
public sealed class SpectralAdrenaline() : HexaCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IgniteHoverTip,
        ExtinguishHoverTip,
        GhostflameHoverTip,
        EnergyHoverTip
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

        HexaCombatState.Get(combatState).ActiveSlotIndex = 0;

        int extinguished = 0;
        foreach (var gf in flames)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (gf.IsIgnited)
            {
                await HexaMechanics.ExtinguishAsync(choiceContext, Owner, gf);
                extinguished++;
            }
        }

        HexaMechanics.RefreshGhostflameVisuals(Owner);

        if (extinguished > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PlayerCmd.GainEnergy(extinguished, Owner);
            if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CardPileCmd.Draw(choiceContext, extinguished, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
