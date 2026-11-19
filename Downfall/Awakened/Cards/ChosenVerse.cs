using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class ChosenVerse()
    : AwakenedCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Unpowered),
        new DynamicVar("Triggers", 2m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int triggers = DynamicVars["Triggers"].IntValue;
        decimal blockAmount = DynamicVars.Block.BaseValue;

        var power = await PowerCmd.Apply<ChosensVersePower>(
            choiceContext,
            Owner.Creature,
            triggers,
            Owner.Creature,
            this);

        power?.RecordEntry(triggers, blockAmount, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
