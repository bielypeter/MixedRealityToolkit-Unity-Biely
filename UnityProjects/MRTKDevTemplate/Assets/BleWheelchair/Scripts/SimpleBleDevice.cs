using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static SimpleBleNative;

public class SimpleBleDevice : IDisposable
{
    private IntPtr _adapter;
    private IntPtr _peripheral;
    private bool disposedValue;

    public bool ScanAndConnectByName(string targetName, int scanTimeMs = 5000)
    {
        uint count = SimpleBleNative.simpleble_adapter_get_count().ToUInt32();
        if (count == 0) throw new Exception("No BLE adapters found");

        if (_adapter != IntPtr.Zero)
        {
            SimpleBleNative.simpleble_adapter_release_handle(_adapter);
            _adapter = IntPtr.Zero;
        }

        if (_peripheral != IntPtr.Zero)
        {
            SimpleBleNative.simpleble_peripheral_release_handle(_peripheral);
            _peripheral = IntPtr.Zero;
        }

        _adapter = SimpleBleNative.simpleble_adapter_get_handle(UIntPtr.Zero);

        if (_adapter != IntPtr.Zero && SimpleBleNative.simpleble_adapter_scan_start(_adapter) == SimpleBleError.Success)
        {
            Debug.Log($"[SimpleBleDevice] Scanning for {scanTimeMs} millisecond...");
            System.Threading.Thread.Sleep(scanTimeMs);
            if (SimpleBleNative.simpleble_adapter_scan_stop(_adapter) == SimpleBleError.Success)
            {
                uint deviceCount = SimpleBleNative.simpleble_adapter_scan_get_results_count(_adapter).ToUInt32();
                Debug.Log($"[SimpleBleDevice] Scan stopped, found {deviceCount} devices");
                for (uint i = 0; i < deviceCount; i++)
                {
                    IntPtr dev = SimpleBleNative.simpleble_adapter_scan_get_results_handle(_adapter, new UIntPtr(i));
                    if (dev == IntPtr.Zero) continue;

                    string name = Marshal.PtrToStringAnsi(SimpleBleNative.simpleble_peripheral_identifier(dev));

                    if (!string.IsNullOrEmpty(name) && name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        _peripheral = dev;
                        Debug.Log($"[SimpleBleDevice] Found target device {name}, connecting...");
                        return SimpleBleNative.simpleble_peripheral_connect(_peripheral) == SimpleBleError.Success;
                    }

                    SimpleBleNative.simpleble_peripheral_release_handle(dev);
                    Debug.Log($"[SimpleBleDevice] Device {name} does not match the target name");
                }
            }
        }
        return false;
    }

    public bool WriteCharacteristic(string serviceUuid, string charUuid, byte[] data)
    {
        if (_peripheral != IntPtr.Zero)
        {
            return SimpleBleNative.simpleble_peripheral_write_request(_peripheral, new SimpleBleUuid(serviceUuid), new SimpleBleUuid(charUuid), data, (UIntPtr)data.Length) == SimpleBleError.Success;
        }
        return false;
    }

    public byte[] ReadCharacteristic(string serviceUuid, string charUuid)
    {
        byte[] result = null;
        if (_peripheral != IntPtr.Zero)
        {
            UIntPtr len = UIntPtr.Zero;
            IntPtr ptr = IntPtr.Zero;
            if (SimpleBleNative.simpleble_peripheral_read(_peripheral, new SimpleBleUuid(serviceUuid), new SimpleBleUuid(charUuid), out ptr, out len) == SimpleBleError.Success)
            {
                int ilen = (int)len.ToUInt32();
                result = new byte[ilen];
                Marshal.Copy(ptr, result, 0, ilen);
            }
            if (ptr != IntPtr.Zero)
            {
                SimpleBleNative.simpleble_free(ptr);
            }
        }
        return result;
    }

    private void Release()
    {
        Debug.Log("[SimpleBleDevice] Releasing BLE resources...");
        if (_peripheral != IntPtr.Zero)
        {
            SimpleBleNative.simpleble_peripheral_disconnect(_peripheral);
            SimpleBleNative.simpleble_peripheral_release_handle(_peripheral);
            _peripheral = IntPtr.Zero;
        }
        if (_adapter != IntPtr.Zero)
        {
            SimpleBleNative.simpleble_adapter_release_handle(_adapter);
            _adapter = IntPtr.Zero;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects)
            Release();
            disposedValue = true;
        }
    }

    ~SimpleBleDevice()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}