using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Poltergeist (ParanormalForm): Whenever you play an Ethereal card, a random enemy loses 6 (8) HP.
/// </summary>
public sealed class ParanormalForm() : HexaCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ParanormalFormPower>(6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ParanormalFormPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(ParanormalFormPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(ParanormalFormPower)].UpgradeValueBy(2m);
    }
}
