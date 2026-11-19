using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Awakened;

internal interface IEnergyDrainListener
{
    Task OnEnergyDrainedAsync(PlayerChoiceContext choiceContext, int energyLost);
}
