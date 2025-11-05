namespace QuantumCore.Game.Constants;

public static class ItemConstants
{
    private const uint JuicePotionsIdStart = 50800;
    private const uint JuicePotionsIdEnd = 50820;

    public const uint OrcStubbornnessId = 70040;

    public static bool IsJuicePotion(uint itemId)
    {
        return itemId is >= JuicePotionsIdStart and <= JuicePotionsIdEnd;
    }
}
