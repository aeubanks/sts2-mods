using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Champ.Potions;

public sealed class UltimateStancePotion : ChampPotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<UltimateFormPower>(1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.UltimateHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await ChampStances.EnterStanceAsync(Owner, ChampStanceKind.Ultimate, choiceContext);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<UltimateFormPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(UltimateFormPower)].BaseValue,
                Owner.Creature,
                null);
        }
    }
}
