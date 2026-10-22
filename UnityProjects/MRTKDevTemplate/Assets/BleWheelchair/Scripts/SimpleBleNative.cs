using System;
using System.Runtime.InteropServices;

internal static class SimpleBleNative
{
    public const string LIB = "simpleble-c";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct SimpleBleUuid
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 37)]
        public string value;

        public SimpleBleUuid(string uuid)
        {
            value = uuid;
        }
    }

    public enum SimpleBleError
    {
        Success = 0,
        Failure = 1
    }

    // Adapter
    [DllImport(LIB)] public static extern IntPtr simpleble_adapter_get_handle(UIntPtr index);
    [DllImport(LIB)] public static extern void simpleble_adapter_release_handle(IntPtr handle);
    [DllImport(LIB)] public static extern UIntPtr simpleble_adapter_get_count();
    [DllImport(LIB)] public static extern SimpleBleError simpleble_adapter_scan_start(IntPtr adapter);
    [DllImport(LIB)] public static extern SimpleBleError simpleble_adapter_scan_stop(IntPtr adapter);
    [DllImport(LIB)] public static extern UIntPtr simpleble_adapter_scan_get_results_count(IntPtr adapter);
    [DllImport(LIB)] public static extern IntPtr simpleble_adapter_scan_get_results_handle(IntPtr adapter, UIntPtr index);

    // Peripheral
    [DllImport(LIB)] public static extern IntPtr simpleble_peripheral_identifier(IntPtr peripheral);
    [DllImport(LIB)] public static extern void simpleble_peripheral_release_handle(IntPtr handle);
    [DllImport(LIB)] public static extern SimpleBleError simpleble_peripheral_connect(IntPtr peripheral);
    [DllImport(LIB)] public static extern SimpleBleError simpleble_peripheral_disconnect(IntPtr peripheral);
    [DllImport(LIB)] public static extern SimpleBleError simpleble_peripheral_is_connected(IntPtr peripheral, [MarshalAs(UnmanagedType.I1)] out bool connected);

    [DllImport(LIB)]
    public static extern SimpleBleError simpleble_peripheral_write_request(
        IntPtr peripheral,
        SimpleBleUuid service,
        SimpleBleUuid characteristic,
        byte[] data,
        UIntPtr data_length
    );

    [DllImport(LIB)]
    public static extern SimpleBleError simpleble_peripheral_read(
        IntPtr peripheral,
        SimpleBleUuid service,
        SimpleBleUuid characteristic,
        out IntPtr data,
        out UIntPtr data_length
    );

    [DllImport(LIB)] public static extern void simpleble_free(IntPtr ptr);

    [DllImport("simpleble")]
    public static extern void SetRegistryClassLoader(IntPtr loader);
}