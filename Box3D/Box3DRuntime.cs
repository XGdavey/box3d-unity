using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Box3D
{
    /// <summary>Installs Box3D's global log/assert hooks so engine diagnostics reach the Unity
    /// console. Runs automatically on play; call <see cref="Install"/> manually from edit-mode
    /// code (tests, tools) if needed.</summary>
    public static class Box3DRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LogDelegate(IntPtr message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AssertDelegate(IntPtr condition, IntPtr fileName, int lineNumber);

        // Keep delegates rooted for the lifetime of the process — the native side stores the
        // function pointers.
        private static readonly LogDelegate LogHandler = OnLog;
        private static readonly AssertDelegate AssertHandler = OnAssert;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                Box3DApi.GetVersion(); // probe: does the native library load on this platform?
            }
            catch (DllNotFoundException)
            {
                Debug.LogError("[Box3D] Native library not found for this platform. Shipped binaries: " +
                               "Windows x64, Linux x64, Android arm64. macOS and iOS must be built from " +
                               "source — see Documentation~/building-natives.md in the package.");
                return;
            }

            if (Box3DApi.IsDoublePrecision)
            {
                Debug.LogError("[Box3D] Native library was built with BOX3D_DOUBLE_PRECISION — " +
                               "this wrapper requires a single-precision build. Struct layouts will not match.");
            }

            UnsafeBindings.b3SetLogFcn(Marshal.GetFunctionPointerForDelegate(LogHandler));
            UnsafeBindings.b3SetAssertFcn(Marshal.GetFunctionPointerForDelegate(AssertHandler));
        }

        [MonoPInvokeCallback(typeof(LogDelegate))]
        private static void OnLog(IntPtr message)
        {
            Debug.Log($"[Box3D] {Marshal.PtrToStringAnsi(message)}");
        }

        [MonoPInvokeCallback(typeof(AssertDelegate))]
        private static int OnAssert(IntPtr condition, IntPtr fileName, int lineNumber)
        {
            Debug.LogError($"[Box3D] Assertion failed: {Marshal.PtrToStringAnsi(condition)} " +
                           $"at {Marshal.PtrToStringAnsi(fileName)}:{lineNumber}");
            return 0;
        }
    }
}
