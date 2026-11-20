using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Champ.Cards;

public sealed class ChampCalledShot()
    : ChampCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool IsCombo => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && ChampStances.IsInStance(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(2),
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ComboHoverTip,
        BerserkerHoverTip,
        DefensiveHoverTip,
        EnergyHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override CardLocation GetResultLocationForCardPlay()
    {
        if (!IsDupe && Owner is not null && ChampStances.IsInBerserker(Owner.Creature))
        {
            return new CardLocation(Owner, PileType.Exhaust, CardPilePosition.Bottom);
        }

        return base.GetResultLocationForCardPlay();
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ChampStances.IsInBerserker(Owner.Creature))
        {
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        }

        if (Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && ChampStances.IsInDefensive(Owner.Creature))
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
