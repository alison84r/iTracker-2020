using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace iTracker
{
    public class WindowHandleInfo
    {
        private delegate bool EnumWindowProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr lParam);

        private readonly IntPtr _mainHandle;

        public WindowHandleInfo(IntPtr handle)
        {
            this._mainHandle = handle;
        }

        public List<IntPtr> GetAllChildHandles()
        {
            List<IntPtr> childHandles = new List<IntPtr>();

            GCHandle gcChildHandlesList = GCHandle.Alloc(childHandles);
            IntPtr pointerChildHandlesList = GCHandle.ToIntPtr(gcChildHandlesList);

            try
            {
                EnumWindowProc childProc = new EnumWindowProc(EnumWindow);
                EnumChildWindows(this._mainHandle, childProc, pointerChildHandlesList);
            }
            finally
            {
                gcChildHandlesList.Free();
            }

            return childHandles;
        }

        private bool EnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            GCHandle gcChildHandlesList = GCHandle.FromIntPtr(lParam);

            if (gcChildHandlesList.Target == null)
            {
                return false;
            }

            List<IntPtr> childHandles = gcChildHandlesList.Target as List<IntPtr>;
            childHandles?.Add(hWnd);

            return true;
        }
    }
}
