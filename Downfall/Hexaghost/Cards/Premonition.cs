using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Choose a card type (Attack, Skill, or Power), then auto-play a random card of that type from your Draw Pile.
/// Exhaust (Upgraded: does not Exhaust).
/// </summary>
public sealed class Premonition() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CardModel[] choices =
        [
            combatState.CreateCard<HexaPremonitionAttack>(Owner),
            combatState.CreateCard<HexaPremonitionSkill>(Owner),
            combatState.CreateCard<HexaPremonitionPower>(Owner)
        ];

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: true);
        CardType? predictedType = chosen switch
        {
            HexaPremonitionAttack => CardType.Attack,
            HexaPremonitionSkill => CardType.Skill,
            HexaPremonitionPower => CardType.Power,
            _ => null
        };
        if (predictedType is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var drawPile = PileType.Draw.GetPile(Owner);
        var matching = drawPile.Cards.Where(c => c.Type == predictedType.Value).ToList();
        if (matching.Count == 0)
            return;

        var picked = Owner.RunState.Rng.CombatCardSelection.NextItem(matching);
        if (picked is null)
            return;

        drawPile.MoveToTopInternal(picked);
        await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, 1, CardPilePosition.Top, forceExhaust: false);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaPremonitionAttack()
    : DownfallCard(0, CardType.Attack, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaPremonitionSkill()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaPremonitionPower()
    : DownfallCard(0, CardType.Power, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}
