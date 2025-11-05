using QuantumCore.API.Game.World;
using QuantumCore.Core.Event;

namespace QuantumCore.Game.Extensions;

public static class PlayerExtensionCountdownEvent
{
    private const int CombatGracePeriodSeconds = 10;
    private const int CombatWaitSeconds = 10;
    private const int NormalWaitSeconds = 3;

    public static void StartCountdownEventCancellable(
        this IPlayerEntity player,
        string initialMessage,
        string countdownMessageTemplate,
        Action onComplete)
    {
        var alreadyStarted = player.CancelCountdownEvent();
        if (alreadyStarted)
        {
            player.SendChatInfo("Cancelled.");
            return;
        }

        player.SendChatInfo(initialMessage);

        int remainingSecondsClosure;
        if (player.MsSinceLastAttacked() < CombatGracePeriodSeconds * 1000)
        {
            remainingSecondsClosure = CombatWaitSeconds;
        }
        else
        {
            remainingSecondsClosure = NormalWaitSeconds;
        }

        // Create countdown timer
        var eventId = EventSystem.EnqueueEvent(() =>
        {
            if (remainingSecondsClosure <= 0)
            {
                onComplete();
                return 0; // Cancel event
            }
            player.SendChatInfo(string.Format(countdownMessageTemplate, remainingSecondsClosure));
            remainingSecondsClosure--;
            
            return 1000;
        }, 0);

        player.SetCountdownEventCancellable(eventId);
    }
}
