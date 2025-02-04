using System.Runtime.InteropServices;
using static ServiceLib.Handler.SysProxy.ProxySettingWindows.InternetConnectionOption;

namespace ServiceLib.Handler.SysProxy
{
    public class ProxySettingWindows
    {
        private const string _regPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        private static bool SetProxyFallback(string? strProxy, string? exceptions, int type)
        {
            if (type == 1)
            {
                WindowsUtils.RegWriteValue(_regPath, "ProxyEnable", 0);
                WindowsUtils.RegWriteValue(_regPath, "ProxyServer", string.Empty);
                WindowsUtils.RegWriteValue(_regPath, "ProxyOverride", string.Empty);
                WindowsUtils.RegWriteValue(_regPath, "AutoConfigURL", string.Empty);
            }
            if (type == 2)
            {
                WindowsUtils.RegWriteValue(_regPath, "ProxyEnable", 1);
                WindowsUtils.RegWriteValue(_regPath, "ProxyServer", strProxy ?? string.Empty);
                WindowsUtils.RegWriteValue(_regPath, "ProxyOverride", exceptions ?? string.Empty);
                WindowsUtils.RegWriteValue(_regPath, "AutoConfigURL", string.Empty);
            }
            else if (type == 4)
            {
                WindowsUtils.RegWriteValue(_regPath, "ProxyEnable", 0);
                WindowsUtils.RegWriteValue(_regPath, "ProxyServer", string.Empty);
                WindowsUtils.RegWriteValue(_regPath, "ProxyOverride", string.Empty);
                WindowsUtils.RegWriteValue(_regPath, "AutoConfigURL", strProxy ?? string.Empty);
            }
            return true;
        }

        /// <summary>
        // set to use no proxy
        /// </summary>
        /// <exception cref="ApplicationException">Error message with win32 error code</exception>
        public static bool UnsetProxy()
        {
            return SetProxy(null, null, 1);
        }

        /// <summary>
        /// Set system proxy settings
        /// </summary>
        /// <param name="strProxy"> proxy address</param>
        /// <param name="exceptions">exception addresses that do not use proxy</param>
        /// <param name="type">type of proxy defined in PerConnFlags
        ///     PROXY_TYPE_DIRECT           = 0x00000001, // direct connection (no proxy)
        ///     PROXY_TYPE_PROXY            = 0x00000002, // via named proxy
        ///     PROXY_TYPE_AUTO_PROXY_URL   = 0x00000004, // autoproxy script URL
        ///     PROXY_TYPE_AUTO_DETECT      = 0x00000008  // use autoproxy detection
        /// </param>
        /// <exception cref="ApplicationException">Error message with win32 error code</exception>
        /// <returns>true: one of connection is successfully updated proxy settings</returns>
        public static bool SetProxy(string? strProxy, string? exceptions, int type)
        {
            try
            {
                // set proxy for LAN
                bool result = SetConnectionProxy(null, strProxy, exceptions, type);
                // set proxy for dial up connections
                var connections = EnumerateRasEntries();
                foreach (var connection in connections)
                {
                    result |= SetConnectionProxy(connection, strProxy, exceptions, type);
                }
                return result;
            }
            catch
            {
                SetProxyFallback(strProxy, exceptions, type);
                return false;
            }
        }

        private static bool SetConnectionProxy(string? connectionName, string? strProxy, string? exceptions, int type)
        {
            InternetPerConnOptionList list = new();

            int optionCount = 1;
            if (type == 1) // No proxy
            {
                optionCount = 1;
            }
            else if (type is 2 or 4) // named proxy or autoproxy script URL
            {
                optionCount = string.IsNullOrEmpty(exceptions) ? 2 : 3;
            }

            int m_Int = (int)PerConnFlags.PROXY_TYPE_DIRECT;
            PerConnOption m_Option = PerConnOption.INTERNET_PER_CONN_FLAGS;
            if (type == 2) // named proxy
            {
                m_Int = (int)(PerConnFlags.PROXY_TYPE_DIRECT | PerConnFlags.PROXY_TYPE_PROXY);
                m_Option = PerConnOption.INTERNET_PER_CONN_PROXY_SERVER;
            }
            else if (type == 4) // autoproxy script url
            {
                m_Int = (int)(PerConnFlags.PROXY_TYPE_DIRECT | PerConnFlags.PROXY_TYPE_AUTO_PROXY_URL);
                m_Option = PerConnOption.INTERNET_PER_CONN_AUTOCONFIG_URL;
            }

            //int optionCount = Utile.IsNullOrEmpty(strProxy) ? 1 : (Utile.IsNullOrEmpty(exceptions) ? 2 : 3);
            InternetConnectionOption[] options = new InternetConnectionOption[optionCount];
            // USE a proxy server ...
            options[0].m_Option = PerConnOption.INTERNET_PER_CONN_FLAGS;
            //options[0].m_Value.m_Int = (int)((optionCount < 2) ? PerConnFlags.PROXY_TYPE_DIRECT : (PerConnFlags.PROXY_TYPE_DIRECT | PerConnFlags.PROXY_TYPE_PROXY));
            options[0].m_Value.m_Int = m_Int;
            // use THIS proxy server
            if (optionCount > 1)
            {
                options[1].m_Option = m_Option;
                options[1].m_Value.m_StringPtr = Marshal.StringToHGlobalAuto(strProxy); // !! remember to deallocate memory 1
                // except for these addresses ...
                if (optionCount > 2)
                {
                    options[2].m_Option = PerConnOption.INTERNET_PER_CONN_PROXY_BYPASS;
                    options[2].m_Value.m_StringPtr = Marshal.StringToHGlobalAuto(exceptions); // !! remember to deallocate memory 2
                }
            }

            // default stuff
            list.dwSize = Marshal.SizeOf(list);
            if (connectionName != null)
            {
                list.szConnection = Marshal.StringToHGlobalAuto(connectionName); // !! remember to deallocate memory 3
            }
            else
            {
                list.szConnection = nint.Zero;
            }
            list.dwOptionCount = options.Length;
            list.dwOptionError = 0;

            int optSize = Marshal.SizeOf(typeof(InternetConnectionOption));
            // make a pointer out of all that ...
            nint optionsPtr = Marshal.AllocCoTaskMem(optSize * options.Length); // !! remember to deallocate memory 4
            // copy the array over into that spot in memory ...
            for (int i = 0; i < options.Length; ++i)
            {
                if (Environment.Is64BitOperatingSystem)
                {
                    nint opt = new(optionsPtr.ToInt64() + (i * optSize));
                    Marshal.StructureToPtr(options[i], opt, false);
                }
                else
                {
                    nint opt = new(optionsPtr.ToInt32() + (i * optSize));
                    Marshal.StructureToPtr(options[i], opt, false);
                }
            }

            list.options = optionsPtr;

            // and then make a pointer out of the whole list
            nint ipcoListPtr = Marshal.AllocCoTaskMem(list.dwSize); // !! remember to deallocate memory 5
            Marshal.StructureToPtr(list, ipcoListPtr, false);

            // and finally, call the API method!
            bool isSuccess = NativeMethods.InternetSetOption(nint.Zero,
               InternetOption.INTERNET_OPTION_PER_CONNECTION_OPTION,
               ipcoListPtr, list.dwSize);
            int returnvalue = 0; // ERROR_SUCCESS
            if (!isSuccess)
            {  // get the error codes, they might be helpful
                returnvalue = Marshal.GetLastPInvokeError();
            }
            else
            {
                // Notify the system that the registry settings have been changed and cause them to be refreshed
                NativeMethods.InternetSetOption(nint.Zero, InternetOption.INTERNET_OPTION_SETTINGS_CHANGED, nint.Zero, 0);
                NativeMethods.InternetSetOption(nint.Zero, InternetOption.INTERNET_OPTION_REFRESH, nint.Zero, 0);
            }

            // FREE the data ASAP
            if (list.szConnection != nint.Zero)
                Marshal.FreeHGlobal(list.szConnection); // release mem 3
            if (optionCount > 1)
            {
                Marshal.FreeHGlobal(options[1].m_Value.m_StringPtr); // release mem 1
                if (optionCount > 2)
                {
                    Marshal.FreeHGlobal(options[2].m_Value.m_StringPtr); // release mem 2
                }
            }
            Marshal.FreeCoTaskMem(optionsPtr); // release mem 4
            Marshal.FreeCoTaskMem(ipcoListPtr); // release mem 5
            if (returnvalue != 0)
            {
                // throw the error codes, they might be helpful
                throw new ApplicationException($"Set Internet Proxy failed with error code: {Marshal.GetLastWin32Error()}");
            }

            return true;
        }

        /// <summary>
        /// Retrieve list of connections including LAN and WAN to support PPPoE connection
        /// </summary>
        /// <returns>A list of RAS connection names. May be empty list if no dial up connection.</returns>
        /// <exception cref="ApplicationException">Error message with win32 error code</exception>
        private static IEnumerable<string> EnumerateRasEntries()
        {
            int entries = 0;
            // attempt to query with 1 entry buffer
            RASENTRYNAME[] rasEntryNames = new RASENTRYNAME[1];
            int bufferSize = Marshal.SizeOf(typeof(RASENTRYNAME));
            rasEntryNames[0].dwSize = Marshal.SizeOf(typeof(RASENTRYNAME));

            uint result = NativeMethods.RasEnumEntries(null, null, rasEntryNames, ref bufferSize, ref entries);
            // increase buffer if the buffer is not large enough
            if (result == (uint)ErrorCode.ERROR_BUFFER_TOO_SMALL)
            {
                rasEntryNames = new RASENTRYNAME[bufferSize / Marshal.SizeOf(typeof(RASENTRYNAME))];
                for (int i = 0; i < rasEntryNames.Length; i++)
                {
                    rasEntryNames[i].dwSize = Marshal.SizeOf(typeof(RASENTRYNAME));
                }

                result = NativeMethods.RasEnumEntries(null, null, rasEntryNames, ref bufferSize, ref entries);
            }
            if (result == 0)
            {
                var entryNames = new List<string>();
                for (int i = 0; i < entries; i++)
                {
                    entryNames.Add(rasEntryNames[i].szEntryName);
                }

                return entryNames;
            }
            throw new ApplicationException($"RasEnumEntries failed with error code: {result}");
        }

        #region WinInet structures

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct InternetPerConnOptionList
        {
            public int dwSize;               // size of the INTERNET_PER_CONN_OPTION_LIST struct
            public nint szConnection;         // connection name to set/query options
            public int dwOptionCount;        // number of options to set/query
            public int dwOptionError;           // on error, which option failed

            //[MarshalAs(UnmanagedType.)]
            public nint options;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct InternetConnectionOption
        {
            private static readonly int Size;
            public PerConnOption m_Option;
            public InternetConnectionOptionValue m_Value;

            static InternetConnectionOption()
            {
                Size = Marshal.SizeOf(typeof(InternetConnectionOption));
            }

            // Nested Types
            [StructLayout(LayoutKind.Explicit)]
            public struct InternetConnectionOptionValue
            {
                // Fields
                [FieldOffset(0)]
                public System.Runtime.InteropServices.ComTypes.FILETIME m_FileTime;

                [FieldOffset(0)]
                public int m_Int;

                [FieldOffset(0)]
                public nint m_StringPtr;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct RASENTRYNAME
            {
                public int dwSize;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = RAS_MaxEntryName + 1)]
                public string szEntryName;

                public int dwFlags;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 1)]
                public string szPhonebookPath;
            }

            // Constants
            public const int RAS_MaxEntryName = 256;

            public const int MAX_PATH = 260; // Standard MAX_PATH value in Windows
        }

        #endregion WinInet structures

        #region WinInet enums

        //
        // options manifests for Internet{Query|Set}Option
        //
        public enum InternetOption : uint
        {
            INTERNET_OPTION_PER_CONNECTION_OPTION = 75,
            INTERNET_OPTION_REFRESH = 37,
            INTERNET_OPTION_SETTINGS_CHANGED = 39
        }

        //
        // Options used in INTERNET_PER_CONN_OPTON struct
        //
        public enum PerConnOption
        {
            INTERNET_PER_CONN_FLAGS = 1, // Sets or retrieves the connection type. The Value member will contain one or more of the values from PerConnFlags
            INTERNET_PER_CONN_PROXY_SERVER = 2, // Sets or retrieves a string containing the proxy servers.
            INTERNET_PER_CONN_PROXY_BYPASS = 3, // Sets or retrieves a string containing the URLs that do not use the proxy server.
            INTERNET_PER_CONN_AUTOCONFIG_URL = 4//, // Sets or retrieves a string containing the URL to the automatic configuration script.
        }

        //
        // PER_CONN_FLAGS
        //
        [Flags]
        public enum PerConnFlags
        {
            PROXY_TYPE_DIRECT = 0x00000001,  // direct to net
            PROXY_TYPE_PROXY = 0x00000002,  // via named proxy
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,  // autoproxy URL
            PROXY_TYPE_AUTO_DETECT = 0x00000008   // use autoproxy detection
        }

        public enum ErrorCode : uint
        {
            ERROR_BUFFER_TOO_SMALL = 603,
            ERROR_INVALID_SIZE = 632
        }

        #endregion WinInet enums

        internal static class NativeMethods
        {
            [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Auto)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool InternetSetOption(nint hInternet, InternetOption dwOption, nint lpBuffer, int dwBufferLength);

            [DllImport("Rasapi32.dll", CharSet = CharSet.Auto)]
            public static extern uint RasEnumEntries(
                string? reserved,          // Reserved, must be null
                string? lpszPhonebook,     // Pointer to full path and filename of phone-book file. If this parameter is NULL, the entries are enumerated from all the remote access phone-book files
                [In, Out] RASENTRYNAME[]? lprasentryname, // Buffer to receive RAS entry names
                ref int lpcb,             // Size of the buffer
                ref int lpcEntries        // Number of entries written to the buffer
            );
        }
    }
}
