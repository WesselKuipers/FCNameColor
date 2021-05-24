using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

    internal class XivApi : IDisposable
    {
        public static int ThreadID => System.Threading.Thread.CurrentThread.ManagedThreadId;

        private readonly DalamudPluginInterface Interface;
        private readonly PluginAddressResolver Address;

        private readonly SetNamePlateDelegate SetNamePlate;
        private readonly Framework_GetUIModuleDelegate GetUIModule;
        private readonly GroupManager_IsObjectIDInPartyDelegate IsObjectIDInParty;
        private readonly GroupManager_IsObjectIDInAllianceDelegate IsObjectIDInAlliance;
        private readonly BattleCharaStore_LookupBattleCharaByObjectIDDelegate LookupBattleCharaByObjectID;

        private static XivApi Instance;

        public static void Initialize(DalamudPluginInterface pluginInterface, PluginAddressResolver address)
        {
            Instance ??= new XivApi(pluginInterface, address);
        }

        private XivApi(DalamudPluginInterface pluginInterface, PluginAddressResolver address)
        {
            Interface = pluginInterface;
            Address = address;

            SetNamePlate = Marshal.GetDelegateForFunctionPointer<SetNamePlateDelegate>(address.AddonNamePlate_SetNamePlatePtr);
            GetUIModule = Marshal.GetDelegateForFunctionPointer<Framework_GetUIModuleDelegate>(address.Framework_GetUIModulePtr);
            IsObjectIDInParty = Marshal.GetDelegateForFunctionPointer<GroupManager_IsObjectIDInPartyDelegate>(address.GroupManager_IsObjectIDInPartyPtr);
            IsObjectIDInAlliance = Marshal.GetDelegateForFunctionPointer<GroupManager_IsObjectIDInAllianceDelegate>(address.GroupManager_IsObjectIDInAlliancePtr);
            LookupBattleCharaByObjectID = Marshal.GetDelegateForFunctionPointer<BattleCharaStore_LookupBattleCharaByObjectIDDelegate>(address.BattleCharaStore_LookupBattleCharaByObjectIDPtr);

            Interface.ClientState.OnLogout += OnLogout_ResetRaptureAtkModule;
        }

        public static void DisposeInstance() => Instance.Dispose();

        public void Dispose()
        {
            Interface.ClientState.OnLogout -= OnLogout_ResetRaptureAtkModule;
        }

        #region RaptureAtkModule

        private static IntPtr _RaptureAtkModulePtr = IntPtr.Zero;

        internal static IntPtr RaptureAtkModulePtr
        {
            get
            {
                if (_RaptureAtkModulePtr == IntPtr.Zero)
                {
                    var frameworkPtr = Instance.Interface.Framework.Address.BaseAddress;
                    var uiModulePtr = Instance.GetUIModule(frameworkPtr);

                    unsafe
                    {
                        var uiModule = *(UIModule*)uiModulePtr;
                        var UIModule_GetRaptureAtkModuleAddress = new IntPtr(uiModule.vfunc[7]);
                        var GetRaptureAtkModule = Marshal.GetDelegateForFunctionPointer<UIModule_GetRaptureAtkModuleDelegate>(UIModule_GetRaptureAtkModuleAddress);
                        _RaptureAtkModulePtr = GetRaptureAtkModule(uiModulePtr);
                    }
                }
                return _RaptureAtkModulePtr;
            }
        }

        private void OnLogout_ResetRaptureAtkModule(object sender, EventArgs evt) => _RaptureAtkModulePtr = IntPtr.Zero;

        #endregion

        #region SeString
        public static IntPtr SeStringToSeStringPtr(SeString seString)
        {
            var bytes = seString.Encode();
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }

        #endregion

        internal static SafeAddonNamePlate GetSafeAddonNamePlate() => new SafeAddonNamePlate(Instance.Interface);

        internal static bool IsLocalPlayer(int actorID) => Instance.Interface.ClientState.LocalPlayer?.ActorId == actorID;

        internal static bool IsPartyMember(int actorID) => Instance.IsObjectIDInParty(Instance.Address.GroupManagerPtr, actorID) == 1;

        internal static bool IsAllianceMember(int actorID) => Instance.IsObjectIDInParty(Instance.Address.GroupManagerPtr, actorID) == 1;

        internal static bool IsPlayerCharacter(int actorID)
        {
            var address = Instance.LookupBattleCharaByObjectID(Instance.Address.BattleCharaStorePtr, actorID);
            if (address == IntPtr.Zero)
                return false;

            return (ObjectKind)Marshal.ReadByte(address + Dalamud.Game.ClientState.Structs.ActorOffsets.ObjectKind) == ObjectKind.Player;
        }

        internal class SafeAddonNamePlate
        {
            private readonly DalamudPluginInterface Interface;

            public IntPtr Pointer => Interface.Framework.Gui.GetUiObjectByName("NamePlate", 1);

            public SafeAddonNamePlate(DalamudPluginInterface pluginInterface)
            {
                Interface = pluginInterface;
            }

            public unsafe SafeNamePlateObject GetNamePlateObject(int index)
            {
                if (Pointer == IntPtr.Zero)
                {
                    PluginLog.Debug($"[{GetType().Name}] AddonNamePlate was null");
                    return null;
                }

                var npObjectArrayPtrPtr = Pointer + Marshal.OffsetOf(typeof(AddonNamePlate), nameof(AddonNamePlate.NamePlateObjectArray)).ToInt32();
                var npObjectArrayPtr = Marshal.ReadIntPtr(npObjectArrayPtrPtr);
                if (npObjectArrayPtr == IntPtr.Zero)
                {
                    PluginLog.Debug($"[{GetType().Name}] NamePlateObjectArray was null");
                    return null;
                }

                var npObjectPtr = npObjectArrayPtr + Marshal.SizeOf(typeof(AddonNamePlate.NamePlateObject)) * index;
                return new SafeNamePlateObject(npObjectPtr, index);
            }
        }

        internal class SafeNamePlateObject
        {
            public readonly IntPtr Pointer;
            public readonly AddonNamePlate.NamePlateObject Data;

            private int _Index;
            private SafeNamePlateInfo _NamePlateInfo;

            public SafeNamePlateObject(IntPtr pointer, int index = -1)
            {
                Pointer = pointer;
                Data = Marshal.PtrToStructure<AddonNamePlate.NamePlateObject>(pointer);
                _Index = index;
            }

            public int Index
            {
                get
                {
                    if (_Index == -1)
                    {
                        var addon = XivApi.GetSafeAddonNamePlate();
                        var npObject0 = addon.GetNamePlateObject(0);
                        if (npObject0 == null)
                        {
                            PluginLog.Debug($"[{GetType().Name}] NamePlateObject0 was null");
                            return -1;
                        }

                        var npObjectBase = npObject0.Pointer;
                        var npObjectSize = Marshal.SizeOf(typeof(AddonNamePlate.NamePlateObject));
                        var index = (Pointer.ToInt64() - npObjectBase.ToInt64()) / npObjectSize;
                        if (index < 0 || index >= 50)
                        {
                            PluginLog.Debug($"[{GetType().Name}] NamePlateObject index was out of bounds");
                            return -1;
                        }

                        _Index = (int)index;
                    }
                    return _Index;
                }
            }

            public SafeNamePlateInfo NamePlateInfo
            {
                get
                {
                    if (_NamePlateInfo == null)
                    {
                        var rapturePtr = XivApi.RaptureAtkModulePtr;
                        if (rapturePtr == IntPtr.Zero)
                        {
                            PluginLog.Debug($"[{GetType().Name}] RaptureAtkModule was null");
                            return null;
                        }

                        var npInfoArrayPtr = XivApi.RaptureAtkModulePtr + Marshal.OffsetOf(typeof(RaptureAtkModule), nameof(RaptureAtkModule.NamePlateInfoArray)).ToInt32();
                        var npInfoPtr = npInfoArrayPtr + Marshal.SizeOf(typeof(RaptureAtkModule.NamePlateInfo)) * Index;
                        _NamePlateInfo = new SafeNamePlateInfo(npInfoPtr);
                    }
                    return _NamePlateInfo;
                }
            }

            public unsafe bool IsVisible => Data.IsVisible;

            public unsafe bool IsLocalPlayer => Data.IsLocalPlayer;
        }

        internal class SafeNamePlateInfo
        {
            public readonly IntPtr Pointer;
            public readonly RaptureAtkModule.NamePlateInfo Data;

            public SafeNamePlateInfo(IntPtr pointer)
            {
                Pointer = pointer;
                Data = Marshal.PtrToStructure<RaptureAtkModule.NamePlateInfo>(Pointer);
            }

            #region Getters

            public IntPtr NameAddress => GetStringPtr(nameof(RaptureAtkModule.NamePlateInfo.Name));

            public string Name => GetString(NameAddress);

            public IntPtr FcNameAddress => GetStringPtr(nameof(RaptureAtkModule.NamePlateInfo.FcName));

            public string FcName => GetString(FcNameAddress);

            public IntPtr TitleAddress => GetStringPtr(nameof(RaptureAtkModule.NamePlateInfo.Title));

            public string Title => GetString(TitleAddress);

            public IntPtr DisplayTitleAddress => GetStringPtr(nameof(RaptureAtkModule.NamePlateInfo.DisplayTitle));

            public string DisplayTitle => GetString(DisplayTitleAddress);

            public IntPtr LevelTextAddress => GetStringPtr(nameof(RaptureAtkModule.NamePlateInfo.LevelText));

            public string LevelText => GetString(LevelTextAddress);

            #endregion

            public bool IsPlayerCharacter() => XivApi.IsPlayerCharacter(Data.ActorID);

            public bool IsPartyMember() => XivApi.IsPartyMember(Data.ActorID);

            public bool IsAllianceMember() => XivApi.IsAllianceMember(Data.ActorID);

            private IntPtr GetStringPtr(string name)
            {
                var namePtr = Pointer + Marshal.OffsetOf(typeof(RaptureAtkModule.NamePlateInfo), name).ToInt32();
                var stringPtrPtr = namePtr + Marshal.OffsetOf(typeof(Utf8String), nameof(Utf8String.StringPtr)).ToInt32();
                var stringPtr = Marshal.ReadIntPtr(stringPtrPtr);
                return stringPtr;
            }

            private string GetString(IntPtr stringPtr) => Marshal.PtrToStringAnsi(stringPtr);
        }

    }
}