using Downfall.Guardian.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Guardian.Relics;

public sealed class GemstoneGun : GuardianRelic
{
    private string _loadedGemsCsv = string.Empty;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    [SavedProperty]
    public string LoadedGemsCsv
    {
        get => _loadedGemsCsv;
        set
        {
            AssertMutable();
            _loadedGemsCsv = value ?? string.Empty;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.GemHoverTip,
        HoverTipFactory.FromCard<GemstoneGunCard>()
    ];

    public override async Task AfterObtained()
    {
        var picked = new List<GemType>(3);
        var ctx = new BlockingPlayerChoiceContext();

        for (int i = 0; i < 3; i++)
        {
            var choices = GuardianGems.CreateDistinctGemChoicesForDeck(Owner, 3, onlyCommon: false, Owner.RunState.Rng.UpFront);
            var chosen = await CardSelectCmd.FromChooseACardScreen(ctx, choices, Owner, canSkip: false);
            if (chosen is GuardianCard { ThisGemType: { } gem })
            {
                picked.Add(gem);
            }
        }

        LoadedGemsCsv = GuardianGems.ToCsv(picked);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { } combatState
            || combatState.RoundNumber != 1
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var gunCard = (GemstoneGunCard)combatState.CreateCard<GemstoneGunCard>(Owner);
        foreach (var gem in GuardianGems.ParseCsv(_loadedGemsCsv))
        {
            gunCard.TrySocketGem(gem);
        }

        Flash();
        await CardPileCmd.AddGeneratedCardToCombat(gunCard, PileType.Hand, Owner);
    }
}
