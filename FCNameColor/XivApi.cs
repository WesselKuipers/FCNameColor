using System.Collections.Generic;

namespace FCNameColor
{
    public struct XivApiSearchResponseCharacter
    {
        public int ID;
        public string Name;
    }

    public struct XivApiCharacterSearchResponse
    {
        public List<XivApiSearchResponseCharacter> Results;
    }
    public struct XivApiMemberSearchResponse
    {
        public List<XivApiSearchResponseCharacter> FreeCompanyMembers;
    }
}