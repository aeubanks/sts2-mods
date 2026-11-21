using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Cards;

public sealed class DemonGlyph()
    : AwakenedCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override bool ShouldGlowGoldInternal => AwakenedSpellbook.IsAwakened(CombatState, Owner);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Immediate", 1m),
        new DynamicVar("OnAwaken", 2m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal immediate = DynamicVars["Immediate"].BaseValue;
        decimal onAwaken = DynamicVars["OnAwaken"].BaseValue;

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, immediate, Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, immediate, Owner.Creature, this);

        if (AwakenedSpellbook.IsAwakened(CombatState, Owner))
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, onAwaken, Owner.Creature, this);
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, onAwaken, Owner.Creature, this);
        }
        else
        {
            await PowerCmd.Apply<DemonGlyphPower>(choiceContext, Owner.Creature, onAwaken, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["OnAwaken"].UpgradeValueBy(1m);
    }
}
