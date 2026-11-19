using BaseLib.Abstracts;
using Godot;

namespace Downfall.Hermit;

public sealed class HermitPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheHermit.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/hermit/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/hermit/charui/text_energy.png";
}
