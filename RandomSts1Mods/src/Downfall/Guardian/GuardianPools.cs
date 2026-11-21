using BaseLib.Abstracts;
using Godot;

namespace Downfall.Guardian;

public sealed class GuardianCardPool : CustomCardPoolModel
{
    public override string Title => TheGuardian.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/guardian/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/guardian/charui/text_energy.png";

    public override Color ShaderColor => TheGuardian.CharacterColor;
    public override Color DeckEntryCardColor => TheGuardian.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class GuardianRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheGuardian.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/guardian/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/guardian/charui/text_energy.png";
}

public sealed class GuardianPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheGuardian.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/guardian/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/guardian/charui/text_energy.png";
}
