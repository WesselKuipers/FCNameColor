using FCNameColor.Utils.GameConfig;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace FCNameColor.Utils
{
    public static class Utils
    {
        public static string BuildPlayername(string name, ConfigOption option)
        {
            var logNameType = GameConfigHelper.Instance.GetNameType(option);
            var result = string.Empty;

            if (logNameType != null)
            {
                var nameSplitted = name.Split(' ');

                if (nameSplitted.Length > 1)
                {
                    var firstName = nameSplitted[0];
                    var lastName = nameSplitted[1];

                    switch (logNameType)
                    {
                        case NameType.FullName:
                            result = $"{firstName} {lastName}";
                            break;
                        case NameType.LastNameShorted:
                            result = $"{firstName} {lastName[..1]}.";
                            break;
                        case NameType.FirstNameShorted:
                            result = $"{firstName[..1]}. {lastName}";
                            break;
                        case NameType.Initials:
                            result = $"{firstName[..1]}. {lastName[..1]}.";
                            break;
                    }
                }
            }

            if (string.IsNullOrEmpty(result))
                result = name;

            return result;
        }
    }
}
