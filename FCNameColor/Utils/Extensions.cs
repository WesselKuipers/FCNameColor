using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace FCNameColor.Utils
{
    public static class Extensions
    {
        public unsafe static void SetSeString(this Utf8String utf8String, SeString seString)
        {
            WriteSeString(utf8String, seString);
        }

        private unsafe static void WriteSeString(Utf8String xivString, SeString s)
        {
            var bytes = s.Encode();
            int i;
            xivString.BufUsed = 0;
            for (i = 0; i < bytes.Length && i < xivString.BufSize - 1; i++)
            {
                *(xivString.StringPtr + i) = bytes[i];
                xivString.BufUsed++;
            }
            *(xivString.StringPtr + i) = 0;
        }
    }
}
