using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

public enum TorchHeadChannel
{
    RandomDoom,
    AoeDamage,
    Block,
    Poison,
    Draw
}

/// <summary>
/// Torch Head (TorchHeadPower): Whenever you play an Attack while you have Temp HP,
/// triggers all accumulated Torch Head channels (Random Doom, AoE Damage, Block, Random Poison, Draw).
/// </summary>
public sealed class TorchHeadPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("RandomDoom", 0m),
        new IntVar("AoeDamage", 0m),
        new IntVar("BlockAmt", 0m),
        new IntVar("PoisonAmt", 0m),
        new IntVar("DrawAmt", 0m),
        new StringVar("EffectsText", "No active effects.")
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.TempHpHoverTip,
        CollectorCard.DoomHoverTip
    ];

    public static async Task<TorchHeadPower?> AddChannelAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        TorchHeadChannel channel,
        int amount,
        CardModel? cardSource)
    {
        if (amount <= 0 || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        var existing = target.GetPower<TorchHeadPower>();
        if (existing is null)
        {
            existing = await PowerCmd.Apply<TorchHeadPower>(choiceContext, target, 1m, target, cardSource);
        }
        else
        {
            existing.Flash();
        }
        existing?.AddChannel(channel, amount);
        return existing;
    }

    public void AddChannel(TorchHeadChannel channel, int amount)
    {
        AssertMutable();
        if (amount <= 0)
            return;

        string key = channel switch
        {
            TorchHeadChannel.RandomDoom => "RandomDoom",
            TorchHeadChannel.AoeDamage => "AoeDamage",
            TorchHeadChannel.Block => "BlockAmt",
            TorchHeadChannel.Poison => "PoisonAmt",
            TorchHeadChannel.Draw => "DrawAmt",
            _ => "RandomDoom"
        };

        DynamicVars[key].BaseValue += amount;
        RefreshEffectsText();
    }

    private void RefreshEffectsText()
    {
        var lines = new List<string>();
        int doom = DynamicVars["RandomDoom"].IntValue;
        int aoe = DynamicVars["AoeDamage"].IntValue;
        int block = DynamicVars["BlockAmt"].IntValue;
        int poison = DynamicVars["PoisonAmt"].IntValue;
        int draw = DynamicVars["DrawAmt"].IntValue;

        if (doom > 0)
            lines.Add($"Apply [blue]{doom}[/blue] [gold]Doom[/gold] to a random enemy.");
        if (poison > 0)
            lines.Add($"Apply [blue]{poison}[/blue] [gold]Poison[/gold] to a random enemy.");
        if (aoe > 0)
            lines.Add($"Deal [blue]{aoe}[/blue] damage to ALL enemies.");
        if (block > 0)
            lines.Add($"Gain [blue]{block}[/blue] [gold]Block[/gold].");
        if (draw > 0)
            lines.Add($"Draw [blue]{draw}[/blue] {(draw == 1 ? "card" : "cards")}.");

        if (DynamicVars["EffectsText"] is StringVar sv)
            sv.StringValue = lines.Count > 0 ? string.Join("\n", lines) : "No active effects.";
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Attack
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || CombatState is null
            || Owner.GetPowerAmount<DownfallTempHpPower>() <= 0)
        {
            return;
        }

        int doom = DynamicVars["RandomDoom"].IntValue;
        int aoe = DynamicVars["AoeDamage"].IntValue;
        int block = DynamicVars["BlockAmt"].IntValue;
        int poison = DynamicVars["PoisonAmt"].IntValue;
        int draw = DynamicVars["DrawAmt"].IntValue;

        if (doom <= 0 && aoe <= 0 && block <= 0 && poison <= 0 && draw <= 0)
            return;

        Flash();

        if (doom > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var tar = CollectorCard.ResolveTarget(Owner.Player!, null);
            if (tar is not null)
                await CollectorMechanics.ApplyDoomAsync(choiceContext, tar, doom, Owner, null);
        }

        if (aoe > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var enemies = CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count > 0)
            {
                await CreatureCmd.Damage(choiceContext, enemies, aoe, ValueProp.Unpowered, Owner);
            }
        }

        if (block > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.GainBlock(Owner, block, ValueProp.Unpowered, null);
        }

        if (poison > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var tar = CollectorCard.ResolveTarget(Owner.Player!, null);
            if (tar is not null)
                await PowerCmd.Apply<PoisonPower>(choiceContext, tar, poison, Owner, null);
        }

        if (draw > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, draw, Owner.Player!);
        }
    }
}
