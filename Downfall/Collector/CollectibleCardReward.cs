using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Collector;

/// <summary>
/// Post-combat Collector reward offering a monster Collectible card for 3 Essence.
/// Serializes cleanly as RewardType.SpecialCard so mid-reward save/reload never throws.
/// </summary>
public sealed class CollectibleCardReward : Reward
{
    private readonly CardModel _card;

    public CardModel Card => _card;

    protected override RewardType RewardType => RewardType.SpecialCard;
    public override int RewardsSetIndex => 4;
    public override bool IsPopulated => true;

    protected override string IconPath => ImageHelper.GetImagePath("ui/reward_screen/reward_icon_special_card.png");

    public override LocString Description
    {
        get
        {
            var loc = new LocString("gameplay_ui", "DOWNFALL-COLLECTIBLE_REWARD.description");
            loc.Add("CardName", _card.Title);
            loc.Add("Cost", CollectorRunState.CollectibleEssenceCost);
            return loc;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard(_card)
    ];

    public CollectibleCardReward(CardModel card, Player player) : base(player)
    {
        _card = card;
    }

    public override void Populate()
    {
    }

    protected override Task<bool> OnSelect()
    {
        var state = CollectorRunState.GetOrCreate(Player);
        if (state.Essence < CollectorRunState.CollectibleEssenceCost)
            return Task.FromResult(false);

        state.ChangeEssence(Player, -CollectorRunState.CollectibleEssenceCost);
        state.AddToCollection(Player, _card);
        return Task.FromResult(true);
    }

    public override void MarkContentAsSeen()
    {
    }

    public override SerializableReward ToSerializable()
    {
        return new SerializableReward
        {
            RewardType = RewardType.SpecialCard,
            SpecialCard = _card.ToSerializable()
        };
    }
}
