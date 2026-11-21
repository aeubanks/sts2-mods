using Downfall.Champ.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Champ.Potions;

public sealed class OpenerPotion : ChampPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.BerserkerHoverTip,
        ChampCard.DefensiveHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.CombatState is not { } combatState || CombatManager.Instance.IsOverOrEnding)
            return;

        CardModel[] berserkerOpeners =
        [
            ModelDb.Card<BerserkersShout>(),
            ModelDb.Card<FocusedDefense>(),
            ModelDb.Card<RecklessLeap>()
        ];

        CardModel[] defensiveOpeners =
        [
            ModelDb.Card<DefensiveShout>(),
            ModelDb.Card<Endure>(),
            ModelDb.Card<RageSigil>()
        ];

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var berserkerChoice = combatState.CreateCard(berserkerOpeners[rng.NextInt(berserkerOpeners.Length)], Owner);
        var defensiveChoice = combatState.CreateCard(defensiveOpeners[rng.NextInt(defensiveOpeners.Length)], Owner);
        berserkerChoice.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
        defensiveChoice.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);

        var choices = new List<CardModel>(2) { berserkerChoice, defensiveChoice };

        var picked = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner);
        if (picked is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int copies = Math.Max(1, DynamicVars.Cards.IntValue);
        for (int i = 0; i < copies; i++)
        {
            var copy = i == 0 ? picked : picked.CreateClone();
            copy.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
        }
    }
}
