using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Guardian.Relics;

public sealed class BottledStasis : BottledGuardianRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    protected override string SelectionPromptKey => "DOWNFALL-BOTTLED_STASIS.selectionScreenPrompt";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [GuardianCard.StasisHoverTip, .. GetBottledCardHoverTips()];

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner
            || combatState.RoundNumber != 1
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || !GuardianMechanics.CanPlaceInStasis(Owner))
        {
            return;
        }

        var drawCard = PileType.Draw.GetPile(Owner).Cards.FirstOrDefault(IsBottledCombatCard);
        if (drawCard is null)
            return;

        Flash();
        await GuardianMechanics.PlaceInStasisAsync(
            choiceContext,
            Owner,
            drawCard,
            isSelfStasis: false,
            placedBeforeTurnOne: true);
    }
}
