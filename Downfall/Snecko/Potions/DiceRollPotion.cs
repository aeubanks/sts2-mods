using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Snecko.Potions;

public sealed class DiceRollPotion : SneckoPotion
{
    public const int ChoiceCount = 3;

    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("RewardCount", 1m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OffclassHoverTip
    ];

    protected override Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.RunState.CurrentRoom is not CombatRoom combatRoom)
            return Task.CompletedTask;

        int rewardCount = DynamicVars["RewardCount"].IntValue;
        var offclassPools = ModelDb.AllCharacters
            .Where(c => c.CardPool != Owner.Character.CardPool)
            .Select(c => c.CardPool)
            .ToArray();
        var rerollOptions = new CardCreationOptions(
            offclassPools,
            CardCreationSource.Encounter,
            CardRarityOddsType.RegularEncounter);

        for (int i = 0; i < rewardCount; i++)
        {
            var cards = SneckoOffclass.GenerateDistinctOffclassDeckCards(
                Owner,
                ChoiceCount,
                c => c.Rarity == CardRarity.Uncommon,
                upgradeAll: true,
                rng: Owner.RunState.Rng.CombatCardGeneration);

            if (cards.Count > 0)
            {
                combatRoom.AddExtraReward(
                    Owner,
                    new CardReward(cards, CardCreationSource.Encounter, Owner, rerollOptions));
            }
        }

        return Task.CompletedTask;
    }
}
