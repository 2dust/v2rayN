using System.Runtime.InteropServices;

namespace v2rayN.Desktop.Common;

internal static class MacAppUtils
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";
    private const nint ActivationPolicyAccessory = 1;

    public static void SetActivationPolicyAccessory()
        => objc_msgSend(
            objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication")),
            sel_registerName("setActivationPolicy:"),
            ActivationPolicyAccessory);

    [DllImport(LibObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(LibObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(nint receiver, nint selector, nint argument);
}
