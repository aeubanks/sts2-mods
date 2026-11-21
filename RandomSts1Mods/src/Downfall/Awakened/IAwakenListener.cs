using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Awakened;

internal interface IAwakenListener
{
    Task OnAwakenedAsync(PlayerChoiceContext choiceContext);
}
