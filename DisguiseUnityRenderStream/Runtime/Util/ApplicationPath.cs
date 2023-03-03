using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Disguise.RenderStream.Utils
{
    /// <summary>
    /// This is a P/Invoke alternative to the System.Diagnostics.Process API which isn't supported in IL2CPP.
    /// </summary>
    static class ApplicationPath
    {
        public static string GetExecutablePath()
        {
            var processHandle = Native.GetCurrentProcess();
            
            uint size = Native.MAX_PATH;
            StringBuilder strBuffer = new StringBuilder((int)size);
            
            if (Native.QueryFullProcessImageName(processHandle, 0, strBuffer, ref size))
                return strBuffer.ToString();

            return null;
        }
        
        static class Native
        {
            const string kerneldll = "kernel32.dll";

            public const int MAX_PATH = 260;
            
            [DllImport(kerneldll, SetLastError = true)]
            public static extern UIntPtr GetCurrentProcess();
            
            [DllImport(kerneldll, CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool QueryFullProcessImageName(
                UIntPtr hProcess,
                uint dwFlags,
                StringBuilder lpExeName,
                ref uint lpdwSize);
        }
    }
}
