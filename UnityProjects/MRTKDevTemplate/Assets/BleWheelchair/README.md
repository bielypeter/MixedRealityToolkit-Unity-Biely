# BleWheelchair – driving the iChair over Bluetooth LE

Copied unchanged from [`JurajVincur/iChair`](https://github.com/JurajVincur/iChair), branch `ichair`, folder
`UnityProjects/MRTKDevTemplate/Assets/BleWheelchair`. This folder only talks to the chair. It has no UI and no
eye-tracking logic. Any interface (ArrowPad, Joystick, …) drives the chair through **one method:
`TestBle.SetXY(x, y)`**.

## What is in here

| File | Role |
|---|---|
| `Scripts/TestBle.cs` | MonoBehaviour to put in the scene. Connects to the chair and keeps sending the current X/Y. |
| `Scripts/SimpleBleDevice.cs` | Small C# wrapper: `ScanAndConnectByName`, `WriteCharacteristic`, `ReadCharacteristic`. |
| `Scripts/SimpleBleNative.cs` | Connects C# to the native SimpleBLE library (`simpleble-c`). |
| `Scripts/Crc16Modbus.cs` | Adds the CRC16/Modbus checksum the chair controller requires on every packet. |
| `Plugins/Windows/x64/*.dll` | Native BLE library for the Editor and Windows builds. |
| `Plugins/Android/**` | Native BLE library plus `simpledroidbridge-release.aar` for the headset (Android). The AAR already declares the `BLUETOOTH*` permissions. |

## How `TestBle` works

1. **Start**
   - On Android, it asks for permissions (fine location, `BLUETOOTH_SCAN`, `BLUETOOTH_CONNECT`).
   - It scans for a device named **`R1D78FEFE`** once per second until it connects. If no chair is found, it keeps scanning in the background without blocking anything.
2. After connecting, it sends two setup commands: `[242,1,0,1,0,100]` and `[242,8,6,1,0,4]`.
3. **Every 50 ms** it sends `[242, 8, 40, 2, 0, x, y]` plus the CRC. The values are whatever `SetXY` set last.
4. **OnDestroy** (also when you leave Play mode) sends `[242,8,7,1,0,4]` and disconnects.

Because the last value is sent again every 50 ms, **the chair keeps doing whatever you last set until you set
something else**. Calling `SetXY` is cheap: it only stores two bytes, so you can call it every frame.

### The X/Y convention

| Axis | 0 | 128 | 255 |
|---|---|---|---|
| X | full left | neutral | full right |
| Y | full backward | neutral | full forward |

**Stop = `SetXY(128, 128)`.**

Values other people have used on the real chair:
- `xkmetk/iChair`: forward 158 (slow) / 168 (fast), backward 98 / 88, turning ±40 around 128.
- The original gaze joystick used 58–198, which is ±70.

Start small (about ±30) and go up from there.

## Scene setup

1. Create an empty GameObject (for example `Wheelchair BLE`) and add **`TestBle`**. Use only one per scene, because it connects to one chair.
2. Add a bridge component (examples below) that reads your interface and calls `SetXY`.
3. **In the Editor**, a Windows PC with a Bluetooth adapter can connect to the chair directly in Play mode. Without the chair, nothing breaks; the values just aren't sent anywhere.
4. **On the headset (Android)**, accept the permission prompt on first launch. If scanning never finds the chair, add `<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />` to the Android manifest. `TestBle` requests this permission at runtime, but the AAR doesn't declare it.

To use a different chair, change the name passed to `ScanAndConnectByName` in `TestBle.Start`.

## Connecting an interface: the general pattern

Every interface needs to produce two numbers from **−1 to 1**: *forward/back* and *turn*. A small bridge component
turns those into bytes:

```csharp
using UnityEngine;

// Base for interface -> wheelchair bridges: turns a normalized command into TestBle bytes.
public abstract class WheelchairBridge : MonoBehaviour
{
    [SerializeField] protected TestBle ble;

    [Tooltip("Maximum offset from 128 forward/backward (keep low while testing).")]
    [SerializeField, Range(0, 127)] private int maxDrive = 30;

    [Tooltip("Maximum offset from 128 for turning.")]
    [SerializeField, Range(0, 127)] private int maxTurn = 30;

    /// <summary>Current command: x = turn (-1 left .. 1 right), y = drive (-1 back .. 1 forward).</summary>
    protected abstract Vector2 ReadCommand();

    private void Update()
    {
        Vector2 c = Vector2.ClampMagnitude(ReadCommand(), 1f);
        int x = Mathf.Clamp(128 + Mathf.RoundToInt(c.x * maxTurn), 0, 255);
        int y = Mathf.Clamp(128 + Mathf.RoundToInt(c.y * maxDrive), 0, 255);
        ble.SetXY(x, y);
    }

    // Never leave the chair driving when the interface goes away.
    private void OnDisable()
    {
        if (ble != null) ble.SetXY(128, 128);
    }
}
```

> Important: use the interface's **current speed/direction**, not its accumulated `Value`. `Value` in both
> `JoystickController` and `GazeArrowPad` is a *position* that keeps adding up over time. The chair needs the
> *rate*: how hard to drive right now.

### Joystick (`Assets/Scripts/Joystick/JoystickController.cs`)

`JoystickController.Velocity` is already the right signal. It is zero inside the dead zone, uses the speed
curve, and returns to zero when the knob is released. Divide it by `MaxSpeed` to get the −1 to 1 range:

```csharp
using MixedReality.Toolkit.Examples.Demos;
using UnityEngine;

public class JoystickWheelchairBridge : WheelchairBridge
{
    [SerializeField] private JoystickController joystick;

    protected override Vector2 ReadCommand()
    {
        float max = Mathf.Max(0.0001f, joystick.MaxSpeed);
        return joystick.Velocity / max;   // x = turn, y = drive
    }
}
```

`JoystickSpeedSlider` changes `MaxSpeed`. With this bridge the slider changes how sensitive the stick feels,
but not the top speed (that is `maxDrive`/`maxTurn`). If the slider should limit the chair's top speed, divide
by a fixed number instead of `MaxSpeed`.

### ArrowPad (`Assets/Scripts/ArrowPad/GazeArrowPad.cs`)

`GazeArrowPad` works out `velocity` and `turnRate` inside `Update()`, but doesn't make them public yet. Add two
properties and set them in `Update()`:

```csharp
// GazeArrowPad.cs – new public properties
public Vector2 Velocity { get; private set; }
public float TurnRate { get; private set; }

// in Update(), right after velocity / turnRate are computed:
Velocity = velocity;
TurnRate = turnRate;
```

Then the bridge is:

```csharp
using MixedReality.Toolkit.Examples.Demos;
using UnityEngine;

public class ArrowPadWheelchairBridge : WheelchairBridge
{
    [SerializeField] private GazeArrowPad pad;

    protected override Vector2 ReadCommand()
    {
        float drive = pad.Velocity.y / Mathf.Max(0.0001f, pad.MaxSpeed);

        // A wheelchair can't move sideways: use the turn buttons, or fall back to the arrows' X direction.
        float turn = pad.TurnRate != 0f
            ? pad.TurnRate / Mathf.Max(0.0001f, pad.TurnSpeed)
            : pad.Velocity.x / Mathf.Max(0.0001f, pad.MaxSpeed);

        return new Vector2(turn, drive);
    }
}
```

The pad's existing behaviour carries over to the chair:
- An active (looked-at) arrow drives while you look at it.
- A locked arrow keeps driving.
- `Stop()` (the middle button) clears the lock, so the command goes back to 0 and the bridge sends 128/128.

## Safety checklist before trying it on the real chair

- [ ] `maxDrive` / `maxTurn` are low (about 30) for the first test.
- [ ] Losing gaze or leaving the interface sends neutral. `OnDisable` above covers disabling. Also check what your interface does when gaze is lost.
- [ ] Have a stop that doesn't depend on looking at a button. `xkmetk/iChair` uses closed eyes: the summed Neon eyelid aperture under a threshold for about 1 s calls `SetXY(128,128)`. See `GUISwitcherPanel` / `GazeZoneJoystickV2` in that repo.
- [ ] Someone stands next to the chair with the physical controller or power switch.
