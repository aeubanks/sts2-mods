using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound.Cards;

public sealed class SplitSpecialist() : SlimeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded ? [SplitHoverTip, CommandHoverTip] : [SplitHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        List<Func<SpecialistChoiceCard>> factories =
        [
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseBronzeSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseHexSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseTorchHeadSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseCultistSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseProtectorSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseChampSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseDrawingSlime>(Owner),
            () => (SpecialistChoiceCard)CombatState.CreateCard<ChooseSlowingSlime>(Owner)
        ];

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        List<CardModel> choices = new(3);
        for (int i = 0; i < 3; i++)
        {
            int idx = rng.NextInt(factories.Count);
            choices.Add(factories[idx]());
            factories.RemoveAt(idx);
        }

        CardModel? picked = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner);
        if (picked is SpecialistChoiceCard choiceCard)
        {
            await choiceCard.SpawnChoiceAsync(choiceContext);
            if (IsUpgraded)
                await SlimeMechanics.CommandAsync(choiceContext, Owner);
        }
    }
}
