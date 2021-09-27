using System;
using System.Runtime.InteropServices;

namespace FCNameColor
{
    public class PlayerPointer : IDisposable
    {
        public IntPtr TitlePtr { get; set; }
        public string Title { get; set; }

        public IntPtr FcPtr { get; set; }
        public string FC { get; set; }

        public IntPtr NamePtr { get; set; }
        public string Name { get; set; }

        public void Dispose()
        {
            if (TitlePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(TitlePtr);
            }

            if (FcPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(FcPtr);
            }

            if (NamePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(NamePtr);
            }
        }
    }
}
