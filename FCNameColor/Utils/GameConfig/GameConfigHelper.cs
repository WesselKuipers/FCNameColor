using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCNameColor.Utils.GameConfig
{
    public enum NameType
    {
        FullName,
        LastNameShorted,
        FirstNameShorted,
        Initials
    }

    public class GameConfigHelper
    {
        private static GameConfigHelper instance = null;
        private unsafe static ConfigModule* configModule = null;

        public static GameConfigHelper Instance
        {
            get
            {
                instance ??= new GameConfigHelper();
                return instance;
            }
        }

        private GameConfigHelper()
        {
            unsafe
            {
                configModule = ConfigModule.Instance();
            }
        }

        private int? GetIntValue(ConfigOption option)
        {
            int? value = null;

            unsafe
            {
                var index = configModule->GetIndex(option);
                if (index.HasValue)
                    value = configModule->GetIntValue(index.Value);
            }

            return value;
        }

        public NameType? GetNameType(ConfigOption option)
        {
            NameType? nameType = null;
            int? value = GetIntValue(option);

            if (value.HasValue)
            {
                switch (value)
                {
                    case 0:
                        nameType = NameType.FullName;
                        break;
                    case 1:
                        nameType = NameType.LastNameShorted;
                        break;
                    case 2:
                        nameType = NameType.FirstNameShorted;
                        break;
                    case 3:
                        nameType = NameType.Initials;
                        break;
                }
            }

            return nameType;
        }
    }
}