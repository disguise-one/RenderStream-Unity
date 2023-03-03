using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Disguise.RenderStream.Utils
{
    // https://www.pinvoke.net/default.aspx/advapi32/regopenkeyex.html
    static class RegistryWrapper
    {
        public static UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
        public static UIntPtr HKEY_CURRENT_USER  = new UIntPtr(0x80000001u);

        public enum ReadRegKeyResult
        {
            Success,
            OpenFailed,
            QueryValueFailed,
            TypeNotSupported
        }

        // Only supports REG_SZ registry value type
        public static ReadRegKeyResult ReadRegKey(UIntPtr rootKey, string keyPath, string valueName, out string value)
        {
            value = null;
            
            if (Native.RegOpenKeyEx(rootKey, keyPath, 0, Native.KEY_READ, out var hKey) == Native.ERROR_SUCCESS)
            {
                try
                {
                    uint size = 1024;
                    StringBuilder keyBuffer = new StringBuilder((int)size);

                    if (Native.RegQueryValueEx(hKey, valueName, IntPtr.Zero, out var type, keyBuffer, ref size) == Native.ERROR_SUCCESS)
                    {
                        if (type == Native.REG_SZ)
                        {
                            value = keyBuffer.ToString();
                            return ReadRegKeyResult.Success;
                        }

                        return ReadRegKeyResult.TypeNotSupported;
                    }

                    return ReadRegKeyResult.QueryValueFailed;
                }
                finally
                {
                    Native.RegCloseKey(hKey);
                }
            }

            return ReadRegKeyResult.OpenFailed;
        }
        
        static class Native
        {
            const string advapidll = "advapi32.dll";

            public const int ERROR_SUCCESS = 0x0;
            
            public const int KEY_READ = 0x20019;

            public const uint REG_NONE = 0x00000000;
            public const uint REG_SZ = 0x00000001;
            
            [DllImport(advapidll, CharSet = CharSet.Auto, SetLastError = true)]
            public static extern int RegOpenKeyEx(
                UIntPtr hKey,
                string subKey,
                int ulOptions,
                int samDesired,
                out UIntPtr hkResult);
            
            [DllImport(advapidll, CharSet = CharSet.Auto, SetLastError = true)]
            public static extern int RegQueryValueEx(
                UIntPtr hKey,
                string lpValueName,
                IntPtr lpReserved,
                out uint lpType,
                StringBuilder lpData,
                ref uint lpcbData);
            
            [DllImport(advapidll, SetLastError = true)]
            public static extern int RegCloseKey(UIntPtr hKey);
        }
    }
}
