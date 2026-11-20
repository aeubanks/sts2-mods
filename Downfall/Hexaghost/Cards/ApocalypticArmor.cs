using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Doomsday (ApocalypticArmor): The next time at least 6 (5) Ghostflames are Ignited,
/// take an extra turn.
/// </summary>
public sealed class ApocalypticArmor() : HexaCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ApocalypticArmorPower>(6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IgniteHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int threshold = DynamicVars[nameof(ApocalypticArmorPower)].IntValue;
        var existing = Owner.Creature.GetPower<ApocalypticArmorPower>();
        if (existing is not null)
        {
            if (threshold < existing.Amount)
                await PowerCmd.ModifyAmount(choiceContext, existing, threshold - existing.Amount, Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<ApocalypticArmorPower>(choiceContext, Owner.Creature, threshold, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(ApocalypticArmorPower)].UpgradeValueBy(-1m);
    }
}
