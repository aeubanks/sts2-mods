using BaseLib.Abstracts;
using Godot;

namespace Packmaster.Character;

public class PackmasterRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => ThePackmaster.CharacterColor;

    public override string BigEnergyIconPath => "res://Packmaster/images/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Packmaster/images/charui/text_energy.png";
}
