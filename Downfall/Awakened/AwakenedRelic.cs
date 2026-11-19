using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Awakened;

[Pool(typeof(AwakenedRelicPool))]
public abstract class AwakenedRelic : DownfallRelic
{
    protected override string CharacterKey => TheAwakenedOne.CharKey;

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner
            && cardPlay.Card.Type == CardType.Power
            && Owner.Creature.CombatState is { } combatState)
        {
            AwakenedSpellbook.TryCheckAwakening(combatState, Owner);
        }
        return base.AfterCardPlayed(choiceContext, cardPlay);
    }
}
