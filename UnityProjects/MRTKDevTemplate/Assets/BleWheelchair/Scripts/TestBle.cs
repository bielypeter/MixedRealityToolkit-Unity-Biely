using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class TestBle : MonoBehaviour, IWheelchairDriver
{
    const string charServ = "69400001-b5a3-f393-e0a9-e50e24dcca99";
    const string charSend = "69400002-b5a3-f393-e0a9-e50e24dcca99";
    //const string charRecv = "69400003-b5a3-f393-e0a9-e50e24dcca99";

    private SimpleBleDevice device = null;

    private CancellationTokenSource cts = new CancellationTokenSource();

    private byte x = 128;
    private byte y = 128;

#if UNITY_ANDROID && !UNITY_EDITOR
    public static Task<bool> WaitForPermissionsAsync(string[] permissions, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        int remaining = permissions.Length;
        bool allGranted = true;

        var callbacks = new PermissionCallbacks();

        void HandleResponse(string perm, bool granted)
        {
            if (!granted)
            {
                allGranted = false;
            }
            remaining--;
            if (remaining == 0)
            {
                tcs.TrySetResult(allGranted);
            }
        }

        callbacks.PermissionGranted += perm => HandleResponse(perm, true);
        callbacks.PermissionDenied += perm => HandleResponse(perm, false);
        callbacks.PermissionDeniedAndDontAskAgain += perm => HandleResponse(perm, false);

        Permission.RequestUserPermissions(permissions, callbacks);

        token.Register(() => tcs.TrySetCanceled());

        return tcs.Task;
    }
#endif

    // Start is called before the first frame update
    async void Start()
    {
        CancellationToken token = cts.Token;
#if UNITY_ANDROID && !UNITY_EDITOR
        await WaitForPermissionsAsync(new string[] { Permission.FineLocation, "android.permission.BLUETOOTH_SCAN", "android.permission.BLUETOOTH_CONNECT" }, token);
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            using (var classLoader = activity.Call<AndroidJavaObject>("getClassLoader"))
            {
                SimpleBleNative.SetRegistryClassLoader(classLoader.GetRawObject());
            }
        }
#endif

        device = new SimpleBleDevice();
        await Task.Run(() =>
        {
            while (token.IsCancellationRequested == false)
            {
                if (device.ScanAndConnectByName("R1D78FEFE") == true)
                {
                    break;
                }
                Thread.Sleep(1000);
            }
        }, token);

        if (token.IsCancellationRequested == false)
        {
            // let's do this after sync so we are sure isConnected reflects the real state
            device.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 1, 0, 1, 0, 100 })); //TODO ensure send until success
            device.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 6, 1, 0, 4 }));

            while (cts.IsCancellationRequested == false)
            {
                await Task.Run(() => WriteXY(x, y), token);
                Debug.Log($"[TestBle] writing x:{x}, y:{y}");
                await Task.Delay(50, token);
            }
        }

        cts.Dispose();
        cts = null;

        device.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 7, 1, 0, 4 }));
        device.Dispose();
        device = null;
    }

    private void WriteXY(byte x, byte y)
    {
        device.WriteCharacteristic(charServ, charSend, Crc16Modbus.AddCrcBytes(new byte[] { 242, 8, 40, 2, 0, x, y }));
    }

    public void SetXY(int x, int y)
    {
        this.x = (byte)x;
        this.y = (byte)y;
    }

    private void OnEnable()
    {
        WheelchairDrivers.Register(this);
    }

    private void OnDisable()
    {
        WheelchairDrivers.Unregister(this);
    }

    private void OnDestroy()
    {
        cts.Cancel();
    }
}
