using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Cards;

public sealed class GemFire() : GuardianCard(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GemHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        var collected = new List<GemType>();
        CollectGemsFromCards(PileType.Hand.GetPile(Owner).Cards, collected);
        CollectGemsFromCards(PileType.Draw.GetPile(Owner).Cards, collected);
        CollectGemsFromCards(PileType.Discard.GetPile(Owner).Cards, collected);
        foreach (var orb in GuardianMechanics.GetStasisOrbs(Owner))
        {
            if (orb.StasisCard is not null)
                CollectGemsFromCard(orb.StasisCard, collected);
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt", null, "heavy_attack.mp3")
            .Execute(choiceContext);

        // Execute non-Synthetic gems first, then Synthetic (Onyx) so debuff removal cleans up after all other effects
        var ordered = collected.Where(g => g != GemType.Synthetic)
            .Concat(collected.Where(g => g == GemType.Synthetic))
            .ToList();

        foreach (var gem in ordered)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            await GuardianGems.TriggerGemAsync(gem, choiceContext, Owner, cardPlay);
        }
    }

    private static void CollectGemsFromCards(IEnumerable<CardModel> cards, List<GemType> target)
    {
        foreach (var card in cards)
            CollectGemsFromCard(card, target);
    }

    private static void CollectGemsFromCard(CardModel card, List<GemType> target)
    {
        if (card is not GuardianCard gc)
            return;

        if (gc.IsGem)
        {
            if (gc.ThisGemType is { } gemType)
                target.Add(gemType);
        }
        else
        {
            target.AddRange(gc.GetSocketedGems());
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
