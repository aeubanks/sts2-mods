using BaseLib.Abstracts;
using Godot;

namespace Downfall.Awakened;

public sealed class AwakenedPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheAwakenedOne.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/awakened/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/awakened/charui/text_energy.png";
}
