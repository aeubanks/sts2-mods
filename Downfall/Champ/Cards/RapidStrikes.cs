using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class RapidStrikes()
    : ChampCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new RepeatVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, "slash_attack.mp3")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var candidates = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this)
                && c.Tags.Contains(CardTag.Strike)
                && !c.EnergyCost.CostsX
                && c.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            .ToList();

        if (candidates.Count > 0)
        {
            var picked = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
            picked?.EnergyCost.SetThisTurn(0, reduceOnly: true);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
