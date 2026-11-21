using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Stoke the Fire (InspiringFlames): Gain 7 (10) Block.
/// For each Ignited Ghostflame, Upgrade a random card in your Hand.
/// </summary>
public sealed class InspiringFlames() : HexaCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IgniteHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        int ignitedCount = HexaMechanics.GetIgnitedCount(Owner);
        var hand = PileType.Hand.GetPile(Owner);
        for (int i = 0; i < ignitedCount; i++)
        {
            var upgradable = hand.Cards.Where(c => c.IsUpgradable).ToList();
            if (upgradable.Count == 0)
                break;

            var pick = Owner.RunState.Rng.CombatCardSelection.NextItem(upgradable);
            if (pick is not null)
                CardCmd.Upgrade(pick);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
