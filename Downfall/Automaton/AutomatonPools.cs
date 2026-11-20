using BaseLib.Abstracts;
using Godot;

namespace Downfall.Automaton;

public sealed class AutomatonCardPool : CustomCardPoolModel
{
    public override string Title => TheAutomaton.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/automaton/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/automaton/charui/text_energy.png";

    public override Color ShaderColor => TheAutomaton.CharacterColor;
    public override Color DeckEntryCardColor => TheAutomaton.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class AutomatonRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheAutomaton.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/automaton/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/automaton/charui/text_energy.png";
}

public sealed class AutomatonPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheAutomaton.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/automaton/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/automaton/charui/text_energy.png";
}
