using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Potions;

public sealed class Tonic : HermitPotion
{
    protected override string IconKey => "tonic";

    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<RuggedPower>(1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<RuggedPower>()
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await PowerCmd.Apply<RuggedPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(RuggedPower)].BaseValue,
            Owner.Creature,
            null);
    }
}
