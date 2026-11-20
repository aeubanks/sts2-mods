using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Guardian.Cards;

public sealed class CompilePackage() : GuardianCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override bool IsPlayable =>
        CombatState is not null && GuardianMechanics.CanPlaceInStasis(Owner);

    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        StasisHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null || !GuardianMechanics.CanPlaceInStasis(Owner))
            return;

        Func<CardModel>[] factories =
        [
            () => CombatState.CreateCard<PackageAutomaton>(Owner),
            () => CombatState.CreateCard<PackageDonuDeca>(Owner),
            () => CombatState.CreateCard<PackageSentry>(Owner),
            () => CombatState.CreateCard<PackageShapes>(Owner),
            () => CombatState.CreateCard<PackageSphere>(Owner),
            () => CombatState.CreateCard<PackageWalker>(Owner),
            () => CombatState.CreateCard<PackageDefect>(Owner)
        ];

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var pool = factories.ToList();
        var choices = new List<CardModel>(3);
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var pkg = pool[idx]();
            pool.RemoveAt(idx);
            if (IsUpgraded)
                CardCmd.Upgrade(pkg);
            choices.Add(pkg);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner);
        if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, chosen);
        }
    }
}
