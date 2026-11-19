using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Powers;

public sealed class DemonGlyphPower : AwakenedPower, IAwakenListener
{
    private bool _activated;

    protected override string IconKey => "demon_glyph_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _activated = false;
    }

    public async Task OnAwakenedAsync(PlayerChoiceContext choiceContext)
    {
        if (_activated || Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        _activated = true;
        int amount = Amount;
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, amount, Owner, null);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, amount, Owner, null);
        await PowerCmd.Remove(this);
    }
}
