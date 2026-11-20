using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Powers;

public sealed class FloatingOrbsPower : GuardianPower
{
    private Dictionary<CardPlay, int> _capturedAmount = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _capturedAmount = [];
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Amount > 0 && cardPlay.Card.Owner?.Creature == Owner)
        {
            _capturedAmount[cardPlay] = Amount;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_capturedAmount.Remove(cardPlay, out int damage)
            || damage <= 0
            || cardPlay.Card.Owner?.Creature != Owner
            || cardPlay.Card.EnergyCost.CostsX
            || cardPlay.Resources.EnergyValue != 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || CombatState is null)
        {
            return;
        }

        var enemies = CombatState.HittableEnemies;
        if (enemies.Count == 0)
            return;

        var target = Owner.Player?.RunState.Rng.CombatTargets.NextItem(enemies) ?? enemies[0];
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            damage,
            ValueProp.Unpowered,
            dealer: Owner,
            cardSource: null,
            cardPlay: null);
    }
}
