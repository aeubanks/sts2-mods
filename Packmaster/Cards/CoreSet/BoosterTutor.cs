using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Packs;

namespace Packmaster.Cards.CoreSet;

public sealed class BoosterTutor()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "booster_tutor";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        var candidates = drawPile.Cards
            .Where(c => PackRegistry.GetForCard(c) is not null)
            .ToList();

        var rng = Owner.RunState.Rng.CombatCardSelection;
        int count = DynamicVars.Cards.IntValue;

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            candidates.RemoveAll(c => c.Pile != drawPile);
            if (candidates.Count == 0)
                break;

            var chosen = candidates[rng.NextInt(candidates.Count)];
            var chosenPack = PackRegistry.GetForCard(chosen);
            candidates.RemoveAll(c => ReferenceEquals(PackRegistry.GetForCard(c), chosenPack));

            await CardPileCmd.Add(chosen, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
