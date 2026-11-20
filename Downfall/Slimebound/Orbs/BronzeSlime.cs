using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Bronze Slime: deals 10 (+Potency) damage to ALL enemies and skips its next attack.
/// </summary>
public sealed class BronzeSlime : SlimeOrb
{
    public bool IsStunned { get; private set; }

    public override int BaseDamage => 10;
    public override Color DarkenedColor => new("#8C6D3F");
    public override Color SpriteTint => IsStunned ? new Color(0.55f, 0.55f, 0.55f, 0.85f) : Colors.White;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        IsStunned = false;
    }

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (IsStunned)
        {
            IsStunned = false;
            RefreshVisuals();
            return;
        }

        IsStunned = true;
        ActivatePassive();
        RefreshVisuals();
        await DealUnpoweredAoeDamageAsync(choiceContext, PassiveVal);
    }
}
