using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            if (TitlePtr != null)
            {
                Marshal.FreeHGlobal(TitlePtr);
            }

            if (FcPtr != null)
            {
                Marshal.FreeHGlobal(FcPtr);
            }

            if (NamePtr != null)
            {
                Marshal.FreeHGlobal(NamePtr);
            }
        }
    }
}
