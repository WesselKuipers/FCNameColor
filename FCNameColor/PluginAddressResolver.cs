using Dalamud.Game;
using Dalamud.Game.Internal;
using System;
using System.Runtime.InteropServices;

namespace FCNameColor
{
    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    internal delegate IntPtr SetNamePlateDelegate(IntPtr addon, bool isPrefixTitle, bool displayTitle, IntPtr title, IntPtr name, IntPtr fcName, int iconID);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    internal delegate IntPtr Framework_GetUIModuleDelegate(IntPtr framework);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    internal delegate IntPtr UIModule_GetRaptureAtkModuleDelegate(IntPtr uiModule);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    internal delegate byte GroupManager_IsObjectIDInPartyDelegate(IntPtr groupManager, int actorId);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    internal delegate byte GroupManager_IsObjectIDInAllianceDelegate(IntPtr groupManager, int actorId);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    internal delegate IntPtr BattleCharaStore_LookupBattleCharaByObjectIDDelegate(IntPtr battleCharaStore, int actorId);

    internal sealed class PluginAddressResolver : BaseAddressResolver
    {
        private const string AddonNamePlate_SetNamePlateSignature = "48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 54 41 56 41 57 48 83 EC 40 44 0F B6 E2";
        internal IntPtr AddonNamePlate_SetNamePlatePtr;

        private const string Framework_GetUIModuleSignature = "E8 ?? ?? ?? ?? 48 8B C8 48 8B 10 FF 92 ?? ?? ?? ?? 48 8B C8 BA ?? ?? ?? ??";
        internal IntPtr Framework_GetUIModulePtr;

        private const string GroupManagerSignature = "48 8D 0D ?? ?? ?? ?? 44 8B E7";
        internal IntPtr GroupManagerPtr;

        private const string GroupManager_IsObjectIDInPartySignature = "E8 ?? ?? ?? ?? EB B8 E8";
        internal IntPtr GroupManager_IsObjectIDInPartyPtr;

        private const string GroupManager_IsObjectIDInAllianceSignature = "33 C0 44 8B CA F6 81 ?? ?? ?? ?? ??";
        internal IntPtr GroupManager_IsObjectIDInAlliancePtr;

        private const string BattleCharaStoreSignature = "8B D0 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 3A";
        internal IntPtr BattleCharaStorePtr;

        private const string BattleCharaStore_LookupBattleCharaByObjectIDSignature = "E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 74 3A 48 8B C8";
        internal IntPtr BattleCharaStore_LookupBattleCharaByObjectIDPtr;

        protected override void Setup64Bit(SigScanner scanner)
        {
            AddonNamePlate_SetNamePlatePtr = scanner.ScanText(AddonNamePlate_SetNamePlateSignature);
            Framework_GetUIModulePtr = scanner.ScanText(Framework_GetUIModuleSignature);
            GroupManagerPtr = scanner.GetStaticAddressFromSig(GroupManagerSignature);
            GroupManager_IsObjectIDInPartyPtr = scanner.ScanText(GroupManager_IsObjectIDInPartySignature);
            GroupManager_IsObjectIDInAlliancePtr = scanner.ScanText(GroupManager_IsObjectIDInAllianceSignature);
            BattleCharaStorePtr = scanner.GetStaticAddressFromSig(BattleCharaStoreSignature);
            BattleCharaStore_LookupBattleCharaByObjectIDPtr = scanner.ScanText(BattleCharaStore_LookupBattleCharaByObjectIDSignature);
        }
    }
}