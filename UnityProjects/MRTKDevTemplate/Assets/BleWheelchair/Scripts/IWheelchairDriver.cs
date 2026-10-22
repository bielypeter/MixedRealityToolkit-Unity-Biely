using System.Collections.Generic;

/// <summary>
/// Anything that can receive wheelchair commands in the chair's own X/Y byte format
/// (0 = full left/backward, 128 = neutral, 255 = full right/forward).
/// Implemented by <see cref="TestBle"/> (real chair) and by the simulated chair.
/// </summary>
public interface IWheelchairDriver
{
    void SetXY(int x, int y);
}

/// <summary>
/// Registry of the active drivers. Interfaces broadcast here, so they don't need to know whether the real chair,
/// the simulated chair, or both are listening (they can live in different scenes).
/// </summary>
public static class WheelchairDrivers
{
    private static readonly List<IWheelchairDriver> drivers = new List<IWheelchairDriver>();

    public static IReadOnlyList<IWheelchairDriver> All => drivers;

    public static void Register(IWheelchairDriver driver)
    {
        if (driver != null && !drivers.Contains(driver))
        {
            drivers.Add(driver);
        }
    }

    public static void Unregister(IWheelchairDriver driver)
    {
        drivers.Remove(driver);
    }

    public static void Broadcast(int x, int y)
    {
        for (int i = 0; i < drivers.Count; i++)
        {
            drivers[i].SetXY(x, y);
        }
    }
}
