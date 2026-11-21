using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Potions;

public sealed class SpawnSlimePotion : SlimePotion
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Slimes", 3m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.SplitHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        int count = DynamicVars["Slimes"].IntValue;
        var rng = Owner.RunState.Rng.CombatCardGeneration;
        for (int i = 0; i < count; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (rng.NextInt(2) == 0)
                await SlimeMechanics.SpawnSpecialistSlimeAsync(choiceContext, Owner);
            else
                await SlimeMechanics.SpawnNormalSlimeAsync(choiceContext, Owner);
        }
    }
}
