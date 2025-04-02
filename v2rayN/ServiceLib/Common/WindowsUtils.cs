using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace ServiceLib.Common;

internal static class WindowsUtils
{
    private static readonly string _tag = "WindowsUtils";

    public static string? RegReadValue(string path, string name, string def)
    {
        RegistryKey? regKey = null;
        try
        {
            regKey = Registry.CurrentUser.OpenSubKey(path, false);
            var value = regKey?.GetValue(name) as string;
            return value.IsNullOrEmpty() ? def : value;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            regKey?.Close();
        }
        return def;
    }

    public static void RegWriteValue(string path, string name, object value)
    {
        RegistryKey? regKey = null;
        try
        {
            regKey = Registry.CurrentUser.CreateSubKey(path);
            if (value.ToString().IsNullOrEmpty())
            {
                regKey?.DeleteValue(name, false);
            }
            else
            {
                regKey?.SetValue(name, value);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        finally
        {
            regKey?.Close();
        }
    }

    public static async Task RemoveTunDevice()
    {
        try
        {
            var sum = MD5.HashData(Encoding.UTF8.GetBytes("wintunsingbox_tun"));
            var guid = new Guid(sum);
            var pnpUtilPath = @"C:\Windows\System32\pnputil.exe";
            var arg = $$""" /remove-device  "SWD\Wintun\{{{guid}}}" """;

            // Try to remove the device
            _ = await Utils.GetCliWrapOutput(pnpUtilPath, arg);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }
}
