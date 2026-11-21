using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Packmaster.Cards.Distortion;

public sealed class CursedCircle()
    : PackmasterCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override string PortraitKey => "cursed_circle";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        int count = PileType.Hand.GetPile(Owner).Cards.Count(c => !ReferenceEquals(c, this));
        if (count <= 0)
            return;

        await PowerCmd.Apply<PiercingWailPower>(choiceContext, Owner.Creature, count, Owner.Creature, this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<PiercingWailPower>(
                choiceContext,
                CombatState.HittableEnemies,
                count,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
