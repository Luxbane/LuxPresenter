using System;
using System.Runtime.InteropServices;

namespace LuxPresenter.Services;

public static class MpvNative
{
    private const string MpvDll = "libmpv-2.dll";

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_create")]
    public static extern IntPtr mpv_create();

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_initialize")]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_terminate_destroy")]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_option_string")]
    public static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command_string")]
    public static extern int mpv_command_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string args);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_property_string")]
    public static extern int mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_get_property_string")]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_free")]
    public static extern void mpv_free(IntPtr data);

    public static string? GetPropertyString(IntPtr ctx, string name)
    {
        if (ctx == IntPtr.Zero) return null;
        IntPtr ptr = mpv_get_property_string(ctx, name);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            mpv_free(ptr);
        }
    }
}
