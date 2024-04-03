// https://github.com/Valkirie/Thunderbolt-Switch/blob/d0d7fd38b1b557dfe2543e37bedcb4db648841d2/DockerForm/VideoController.cs
// https://github.com/rzak23/Utilities/blob/master/Get_Ghost_Devices.ps1
// https://github.com/alexjebens/ghost-network-adapters
using System.Runtime.InteropServices;
using System.Text;

namespace v2rayN.Handler
{
    internal class NetAdapterSettings
    {
        public static bool RemoveHiddenAdapter(string interfaceName)
        {
            string FriendlyName = "sing-tun Tunnel";
            Guid networkGuid = new("{4d36e972-e325-11ce-bfc1-08002be10318}");
            // Get all devices of {4d36e972-e325-11ce-bfc1-08002be10318}
            var devices = SetupAPI.SetupDiGetClassDevs(ref networkGuid, IntPtr.Zero, IntPtr.Zero, (int)DiGetClassFlags.DIGCF_PROFILE);

            // Initialise Struct to hold device info Data
            SP_DEVINFO_DATA deviceinfo = new();
            deviceinfo.cbSize = (uint)Marshal.SizeOf(deviceinfo);

            DEVPROPKEY devPropKey = new()
            {
                fmtid = new("{3361c968-2f2e-4660-b47e-699cdc4c32b9}"),
                pid = 3
            };
            UnicodeEncoding unicodeEncoding = new();
            bool result = true;
            // Enumerate Devices
            for (uint deviceIndex = 0; SetupAPI.SetupDiEnumDeviceInfo(devices, deviceIndex, ref deviceinfo); deviceIndex++)
            {
                SetupAPI.SetupDiGetDeviceProperty(devices, ref deviceinfo, ref devPropKey, out uint propTypeDK, null, 0, out int propBufferDKSize, 0);
                byte[] propBufferDK = new byte[propBufferDKSize];

                var success = SetupAPI.SetupDiGetDeviceProperty(devices, ref deviceinfo, ref devPropKey, out propTypeDK, propBufferDK, propBufferDKSize, out propBufferDKSize, 0);
                if (!success)
                {
                    continue;
                }
                var interfaceDKName = unicodeEncoding.GetString(propBufferDK);
                if (interfaceDKName.Length > 1)
                {
                    interfaceDKName = interfaceDKName.Substring(0, interfaceDKName.Length - 1);
                }
                if (!interfaceName.Equals(interfaceDKName))
                {
                    continue;
                }
                result &= RemoveHiddenAdapter(devices, ref deviceinfo, FriendlyName);
            }
            if (!result)
            {
                // throw the error codes, they might be helpful
                throw new ApplicationException($"Remove Hidden Adapter failed with error code: {Marshal.GetLastWin32Error()}");
            }
            return true;
        }
        private static bool RemoveHiddenAdapter(IntPtr devices, ref SP_DEVINFO_DATA deviceinfo, string FriendlyName)
        {
            // Will contain an enum depending on the type of the registry Property, not used but required for call
            // Buffer is initially null and buffer size 0 so that we can get the required Buffer size first
            SetupAPI.SetupDiGetDeviceRegistryProperty(devices, ref deviceinfo, (uint)SetupDiGetDeviceRegistryPropertyEnum.SPDRP_FRIENDLYNAME, out uint propTypeFN, null, 0, out uint propBufferFNSize);
            byte[] propBufferFN = new byte[propBufferFNSize];

            SetupAPI.SetupDiGetDeviceRegistryProperty(devices, ref deviceinfo, (uint)SetupDiGetDeviceRegistryPropertyEnum.SPDRP_DEVICEDESC, out uint propTypeDD, null, 0, out uint propBufferSizeDD);

            SetupAPI.SetupDiGetDeviceRegistryProperty(devices, ref deviceinfo, (uint)SetupDiGetDeviceRegistryPropertyEnum.SPDRP_INSTALL_STATE, out uint propTypeIS, null, 0, out uint propBufferSizeIS);

            bool result = true;
            string FName;
            UnicodeEncoding unicodeEncoding = new();

            if (!SetupAPI.SetupDiGetDeviceRegistryProperty(devices, ref deviceinfo, (uint)SetupDiGetDeviceRegistryPropertyEnum.SPDRP_FRIENDLYNAME, out propTypeFN, propBufferFN, propBufferFNSize, out propBufferFNSize))
            {
                byte[] propBufferDD = new byte[propBufferSizeDD];
                var success = SetupAPI.SetupDiGetDeviceRegistryProperty(devices, ref deviceinfo, (uint)SetupDiGetDeviceRegistryPropertyEnum.SPDRP_DEVICEDESC, out propTypeDD, propBufferDD, propBufferSizeDD, out propBufferSizeDD);
                if (!success)
                {
                    throw new ApplicationException($"Get The DEVPKEY_Device_FriendlyName device property represents the friendly name of a device instance failed with error code: {Marshal.GetLastWin32Error()}");
                }
                FName = unicodeEncoding.GetString(propBufferDD);
                if (FName.Length > 1)
                {
                    FName = FName.Substring(0, FName.Length - 1);
                }
            }
            else
            {
                // Get Unicode String from Buffer
                FName = unicodeEncoding.GetString(propBufferFN);
                // The friendly Name ends with a weird character
                FName = FName.Substring(0, FName.Length - 1);

            }
            byte[] propBufferIS = new byte[propBufferSizeIS];
            var installstatus = SetupAPI.SetupDiGetDeviceRegistryProperty(devices, ref deviceinfo, (uint)SetupDiGetDeviceRegistryPropertyEnum.SPDRP_INSTALL_STATE, out propTypeIS, propBufferIS, propBufferSizeIS, out propBufferSizeIS);
            if (FriendlyName.Equals(FName) && !installstatus)
            {
                result &= SetupAPI.SetupDiRemoveDevice(devices, ref deviceinfo);
            }
            result &= SetupAPI.SetupDiDestroyDeviceInfoList(devices);
            if (!result)
            {
                // throw the error codes, they might be helpful
                throw new ApplicationException($"Remove Hidden Adapter failed with error code: {Marshal.GetLastWin32Error()}");
            }
            return true;
        }
    }

    #region SetupApi structures
    [StructLayout(LayoutKind.Sequential)]
    internal struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid classGuid;
        public uint devInst;
        public IntPtr reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }
    #endregion SetupApi structures

    #region SetupApi enums
    [Flags]
    public enum DiGetClassFlags : uint
    {
        DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
        DIGCF_PRESENT = 0x00000002,
        DIGCF_ALLCLASSES = 0x00000004,
        DIGCF_PROFILE = 0x00000008,
        DIGCF_DEVICEINTERFACE = 0x00000010,
    }

    public enum SetupDiGetDeviceRegistryPropertyEnum : uint
    {
        SPDRP_DEVICEDESC = 0x00000000, // DeviceDesc (R/W)
        SPDRP_HARDWAREID = 0x00000001, // HardwareID (R/W)
        SPDRP_COMPATIBLEIDS = 0x00000002, // CompatibleIDs (R/W)
        SPDRP_UNUSED0 = 0x00000003, // unused
        SPDRP_SERVICE = 0x00000004, // Service (R/W)
        SPDRP_UNUSED1 = 0x00000005, // unused
        SPDRP_UNUSED2 = 0x00000006, // unused
        SPDRP_CLASS = 0x00000007, // Class (R--tied to ClassGUID)
        SPDRP_CLASSGUID = 0x00000008, // ClassGUID (R/W)
        SPDRP_DRIVER = 0x00000009, // Driver (R/W)
        SPDRP_CONFIGFLAGS = 0x0000000A, // ConfigFlags (R/W)
        SPDRP_MFG = 0x0000000B, // Mfg (R/W)
        SPDRP_FRIENDLYNAME = 0x0000000C, // FriendlyName (R/W)
        SPDRP_LOCATION_INFORMATION = 0x0000000D, // LocationInformation (R/W)
        SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E, // PhysicalDeviceObjectName (R)
        SPDRP_CAPABILITIES = 0x0000000F, // Capabilities (R)
        SPDRP_UI_NUMBER = 0x00000010, // UiNumber (R)
        SPDRP_UPPERFILTERS = 0x00000011, // UpperFilters (R/W)
        SPDRP_LOWERFILTERS = 0x00000012, // LowerFilters (R/W)
        SPDRP_BUSTYPEGUID = 0x00000013, // BusTypeGUID (R)
        SPDRP_LEGACYBUSTYPE = 0x00000014, // LegacyBusType (R)
        SPDRP_BUSNUMBER = 0x00000015, // BusNumber (R)
        SPDRP_ENUMERATOR_NAME = 0x00000016, // Enumerator Name (R)
        SPDRP_SECURITY = 0x00000017, // Security (R/W, binary form)
        SPDRP_SECURITY_SDS = 0x00000018, // Security (W, SDS form)
        SPDRP_DEVTYPE = 0x00000019, // Device Type (R/W)
        SPDRP_EXCLUSIVE = 0x0000001A, // Device is exclusive-access (R/W)
        SPDRP_CHARACTERISTICS = 0x0000001B, // Device Characteristics (R/W)
        SPDRP_ADDRESS = 0x0000001C, // Device Address (R)
        SPDRP_UI_NUMBER_DESC_FORMAT = 0X0000001D, // UiNumberDescFormat (R/W)
        SPDRP_DEVICE_POWER_DATA = 0x0000001E, // Device Power Data (R)
        SPDRP_REMOVAL_POLICY = 0x0000001F, // Removal Policy (R)
        SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x00000020, // Hardware Removal Policy (R)
        SPDRP_REMOVAL_POLICY_OVERRIDE = 0x00000021, // Removal Policy Override (RW)
        SPDRP_INSTALL_STATE = 0x00000022, // Device Install State (R)
        SPDRP_LOCATION_PATHS = 0x00000023, // Device Location Paths (R)
        SPDRP_BASE_CONTAINERID = 0x00000024  // Base ContainerID (R)
    }

    #endregion SetupApi enums

    internal static class SetupAPI
    {
        // 1st form using a ClassGUID only, with Enumerator = IntPtr.Zero
        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetupDiGetClassDevs(
           ref Guid ClassGuid,
           IntPtr Enumerator,
           IntPtr hwndParent,
           int Flags
        );

        // 2nd form uses an Enumerator only, with ClassGUID = IntPtr.Zero
        [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SetupDiGetClassDevs(
           IntPtr ClassGuid,
           string Enumerator,
           IntPtr hwndParent,
           int Flags
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet,
            uint MemberIndex,
            ref SP_DEVINFO_DATA DeviceInfoData
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(
            IntPtr DeviceInfoSet
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            uint property,
            out UInt32 propertyRegDataType,
            byte[]? propertyBuffer,
            uint propertyBufferSize,
            out UInt32 requiredSize
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceProperty(
            IntPtr deviceInfo,
            ref SP_DEVINFO_DATA deviceInfoData,
            ref DEVPROPKEY propkey,
            out uint propertyDataType,
            byte[]? propertyBuffer,
            int propertyBufferSize,
            out int requiredSize,
            uint flags
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiRemoveDevice(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);
    }
}