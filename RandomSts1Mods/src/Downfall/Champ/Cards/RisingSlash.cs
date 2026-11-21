using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class RisingSlash()
    : ChampCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && WasLastCardPlayedFinisher();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        FinisherHoverTip
    ];

    private bool WasLastCardPlayedFinisher(CardPlay? currentPlay = null)
    {
        if (Owner is null || CombatState is null)
            return false;

        CardPlay? previous = null;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay.Card.Owner != Owner)
                continue;

            if (currentPlay is not null && ReferenceEquals(entry.CardPlay, currentPlay))
                return previous?.Card is ChampCard { IsFinisher: true };

            previous = entry.CardPlay;
        }

        return previous?.Card is ChampCard { IsFinisher: true };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = WasLastCardPlayedFinisher(cardPlay) ? 2 : 1;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, "slash_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
