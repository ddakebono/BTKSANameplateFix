using Harmony;
using Il2CppSystem.Text;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UIExpansionKit.API;
using UnhollowerRuntimeLib;
using UnityEngine;
using VRC;
using VRC.Core;
using VRC.SDKBase;

namespace BTKSANameplateMod
{
    public static class BuildInfo
    {
        public const string Name = "BTKSANameplateMod"; // Name of the Mod.  (MUST BE SET)
        public const string Author = "DDAkebono#0001"; // Author of the Mod.  (Set as null if none)
        public const string Company = "BTK-Development"; // Company that made the Mod.  (Set as null if none)
        public const string Version = "2.0.0"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        public static BTKSANameplateMod instance;

        public HarmonyInstance harmony;

        public bool isInit = false;

        private string settingsCategory = "BTKSANameplateFix";
        private string hiddenCustomSetting = "enableHiddenCustomNameplates";
        private string hideFriendsNameplates = "hideFriendsNameplates";
        private string showTrustColours = "showTrustColours";

        //Save prefs copy to compare for ReloadAllAvatars
        bool hiddenCustomLocal = false;
        bool hideFriendsLocal = false;
        bool showTrustColoursLocal = false;

        AssetBundle bundle;
        Material npUIMaterial;

        List<string> hiddenNameplateUserIDs = new List<string>();

        public override void VRChat_OnUiManagerInit()
        {
            Log("BTK Standalone: Nameplate Mod - Starting up");

            instance = this;

            if (MelonHandler.Mods.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                MelonLogger.Log("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                MelonLogger.LogError("BTKSANameplateMod has not started up! (BTKCompanion Running)");
                return;
            }

            MelonPrefs.RegisterCategory(settingsCategory, "Nameplate Mod");
            MelonPrefs.RegisterBool(settingsCategory, hiddenCustomSetting, false, "Enable Custom Nameplates (Not ready)");
            MelonPrefs.RegisterBool(settingsCategory, hideFriendsNameplates, false, "Hide Friends Nameplates");
            MelonPrefs.RegisterBool(settingsCategory, showTrustColours, true, "Show Trust Colours");

            //Register our menu button
            if (MelonHandler.Mods.Any(x => x.Info.Name.Equals("UI Expansion Kit", StringComparison.OrdinalIgnoreCase)))
                ExpansionKitApi.GetExpandedMenu(ExpandedMenu.UserQuickMenu).AddSimpleButton("Toggle Nameplate Visibility", ToggleNameplateVisiblity);

            //Initalize Harmony
            harmony = HarmonyInstance.Create("BTKStandalone");

            harmony.Patch(typeof(VRCAvatarManager).GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance), null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnVRCAMAwake", BindingFlags.NonPublic | BindingFlags.Static)));

            Log("Loading Nameplate Assets");
            loadAssets();

            Log("Loading HiddenNameplateUserIDs from file", true);
            LoadHiddenNameplateFromFile();
            //Load the settings to the local copy to compare with SettingsApplied
            getPrefsLocal();
        }


        public override void OnModSettingsApplied()
        {
            if (hiddenCustomLocal != MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting) || showTrustColoursLocal != MelonPrefs.GetBool(settingsCategory, showTrustColours) || hideFriendsLocal != MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates))
                VRCPlayer.field_Internal_Static_VRCPlayer_0.Method_Public_Void_Boolean_0();

            getPrefsLocal();
        }

        public void OnUpdatePlayer(Player player)
        {
            if (ValidatePlayerAvatar(player))
            {
                GDBUser user = new GDBUser(player);
                if (!player.name.Contains("Local"))
                {
                    if (user.vrcPlayer.field_Internal_VRCPlayer_0.nameplate == null)
                        return;

                    PlayerNameplate nameplate = user.vrcPlayer.field_Internal_VRCPlayer_0.nameplate;

                    resetNameplate(nameplate);

                    ////
                    /// Player nameplate checks
                    ////
                    ///

                    //Check if the Nameplate should be hidden
                    if (hiddenNameplateUserIDs.Contains(player.field_Private_APIUser_0.id))
                    {
                        Log("Hiding nameplate - HiddenSet", true);
                        nameplate.uiContents.transform.localScale = Vector3.zero;
                        return;
                    }

                    if (showTrustColoursLocal)
                    {
                        Color trustColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(player.field_Private_APIUser_0);

                        Log("Setting nameplate colour", true);

                        ApplyNameplateColour(nameplate, trustColor, trustColor, Color.red);
                    }

                    if (hiddenCustomLocal)
                    {

                        Transform avatarRoot = player.field_Internal_VRCPlayer_0.field_Internal_GameObject_0.transform;
                        for (int i = 0; i < avatarRoot.childCount; i++)
                        {
                            GameObject child = avatarRoot.GetChild(i).gameObject;
                            if (!child.active && child.name.StartsWith("BTKNameplate"))
                            {
                                string[] values = child.name.Split(':');

                                if (values.Length == 4)
                                {
                                    Color32 bgColor;
                                    Color32 iconBGColor;
                                    Color32 textColor;

                                    Log($"Colour String: {values[1].ToUpper().Trim()},{values[2].ToUpper().Trim()},{values[3].ToUpper().Trim()} ");

                                    if (ColorUtility.DoTryParseHtmlColor(values[1].ToUpper().Trim(), out bgColor) && ColorUtility.DoTryParseHtmlColor(values[2].ToUpper().Trim(), out iconBGColor) && ColorUtility.DoTryParseHtmlColor(values[3].ToUpper().Trim(), out textColor))
                                    {
                                        //Colour string valid!
                                        ApplyNameplateColour(nameplate, bgColor, iconBGColor, Color.red);
                                    }
                                    else
                                    {
                                        Log("Invalid colour string detected, unable to apply colour string to nameplate!");
                                    }
                                }

                                SkinnedMeshRenderer iconComponent = child.GetComponent<SkinnedMeshRenderer>();
                                if (iconComponent != null)
                                {
                                    Log("RendererFound");
                                    ApplyIcon(iconComponent.material.mainTexture, nameplate);
                                }

                                break;
                            }
                        }
                    }

                    //Disable nameplates on friends
                    if (hideFriendsLocal)
                    {
                        if (player.field_Private_APIUser_0 != null)
                        {
                            if (APIUser.IsFriendsWith(player.field_Private_APIUser_0.id))
                            {
                                Log("Hiding Nameplate - FriendsHide", true);
                                nameplate.uiContents.transform.localScale = Vector3.zero;
                            }
                        }
                    }

                }
            }
        }

        private void resetNameplate(PlayerNameplate nameplate)
        {
            nameplate.uiContents.transform.localScale = new Vector3(1, 1, 1);

            ApplyNameplateColour(nameplate, Color.white, Color.white, Color.white);

        }

        private void ApplyIcon(Texture texture, PlayerNameplate nameplate)
        {
            nameplate.uiIconBackground.enabled = true;
            nameplate.uiUserImage.enabled = true;
            nameplate.uiUserImageContainer.SetActive(true);

            nameplate.uiUserImage.texture = texture;
        }

        private void ApplyNameplateColour(PlayerNameplate nameplate, Color bgColor, Color iconBGColor, Color textColor)
        {
            Log("Apply colours", true);

            nameplate.uiNameBackground.material = npUIMaterial;
            nameplate.uiQuickStatsBackground.material = npUIMaterial;
            nameplate.uiIconBackground.material = npUIMaterial;

            Color oldBGColor = nameplate.uiNameBackground.color;
            Color oldIconBGColor = nameplate.uiIconBackground.color;
            Color oldQSBGColor = nameplate.uiQuickStatsBackground.color;
            //Color oldTextColor = nameplate.uiName.faceColor;

            //Copy the alpha values
            Color quickStatsBGColor = bgColor;
            iconBGColor.a = oldIconBGColor.a;
            bgColor.a = oldBGColor.a;
            quickStatsBGColor.a = oldQSBGColor.a;
            //textColor.a = oldTextColor.a;

            nameplate.uiIconBackground.color = iconBGColor;
            nameplate.uiNameBackground.color = bgColor;
            nameplate.uiQuickStatsBackground.color = quickStatsBGColor;
            //nameplate.uiName.color = textColor;
        }

        private void ToggleNameplateVisiblity()
        {
            if (!hiddenNameplateUserIDs.Contains(QuickMenu.prop_QuickMenu_0.field_Private_APIUser_0.id))
                hiddenNameplateUserIDs.Add(QuickMenu.prop_QuickMenu_0.field_Private_APIUser_0.id);
            else
                hiddenNameplateUserIDs.Remove(QuickMenu.prop_QuickMenu_0.field_Private_APIUser_0.id);

            SaveHiddenNameplateFile();
            OnUpdatePlayer(getPlayerFromPlayerlist(QuickMenu.prop_QuickMenu_0.field_Private_APIUser_0.id));
        }

        private void SaveHiddenNameplateFile()
        {
            StringBuilder builder = new StringBuilder();
            foreach (string id in hiddenNameplateUserIDs)
            {
                builder.Append(id);
                builder.AppendLine();
            }
            File.WriteAllText("UserData\\BTKHiddenNameplates.txt", builder.ToString());
        }

        private void LoadHiddenNameplateFromFile()
        {
            if (File.Exists("UserData\\BTKHiddenNameplates.txt"))
            {
                hiddenNameplateUserIDs.Clear();

                string[] lines = File.ReadAllLines("UserData\\BTKHiddenNameplates.txt");

                foreach (string line in lines)
                {
                    if (!String.IsNullOrWhiteSpace(line))
                        hiddenNameplateUserIDs.Add(line);
                }
            }
        }

        private void getPrefsLocal()
        {
            hiddenCustomLocal = MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting);
            hideFriendsLocal = MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates);
            showTrustColoursLocal = MelonPrefs.GetBool(settingsCategory, showTrustColours);
        }

        private void loadAssets()
        {
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSANameplateMod.nmasset"))
            {
                Log("Loaded Embedded resource");
                using (var tempStream = new MemoryStream((int)assetStream.Length))
                {
                    assetStream.CopyTo(tempStream);

                    bundle = AssetBundle.LoadFromMemory_Internal(tempStream.ToArray(), 0);
                    bundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }

            if (bundle != null)
            {
                npUIMaterial = bundle.LoadAsset_Internal("NameplateMat", Il2CppType.Of<Material>()).Cast<Material>();
                npUIMaterial.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
        }

        private static void OnVRCAMAwake(VRCAvatarManager __instance)
        {
            Log("Detected new AvatarManager, setting up delegate", true);

            var d = __instance.field_Internal_MulticastDelegateNPublicSealedVoGaVRBoUnique_0;
            VRCAvatarManager.MulticastDelegateNPublicSealedVoGaVRBoUnique converted = new Action<GameObject, VRC_AvatarDescriptor, bool>(OnAvatarInit);
            d = d == null ? converted : Il2CppSystem.Delegate.Combine(d, converted).Cast<VRCAvatarManager.MulticastDelegateNPublicSealedVoGaVRBoUnique>();
            __instance.field_Internal_MulticastDelegateNPublicSealedVoGaVRBoUnique_0 = d;

            var d1 = __instance.field_Internal_MulticastDelegateNPublicSealedVoGaVRBoUnique_1;
            VRCAvatarManager.MulticastDelegateNPublicSealedVoGaVRBoUnique converted1 = new Action<GameObject, VRC_AvatarDescriptor, bool>(OnAvatarInit);
            d1 = d1 == null ? converted1 : Il2CppSystem.Delegate.Combine(d1, converted1).Cast<VRCAvatarManager.MulticastDelegateNPublicSealedVoGaVRBoUnique>();
            __instance.field_Internal_MulticastDelegateNPublicSealedVoGaVRBoUnique_1 = d1;

            Log("Finished setup", true);

        }

        public static void OnAvatarInit(GameObject go, VRC_AvatarDescriptor avatarDescriptor, bool state)
        {

            if (avatarDescriptor != null)
            {
                foreach (Player player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
                {
                    if (player.field_Private_APIUser_0 == null)
                        continue;

                    VRCPlayer vrcPlayer = player.field_Internal_VRCPlayer_0;
                    if (vrcPlayer == null)
                        continue;

                    VRCAvatarManager vrcAM = vrcPlayer.prop_VRCAvatarManager_0;
                    if (vrcAM == null)
                        continue;

                    VRC_AvatarDescriptor descriptor = vrcAM.prop_VRC_AvatarDescriptor_0;
                    if ((descriptor == null) || (descriptor != avatarDescriptor))
                        continue;

                    BTKSANameplateMod.instance.OnUpdatePlayer(player);
                    break;
                }
            }
        }

        public static void Log(string log, bool dbg = false)
        {
            if (!Imports.IsDebugMode() && dbg)
                return;

            MelonLogger.Log(log);
        }

        public static Player getPlayerFromPlayerlist(string userID)
        {
            foreach (var player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
            {
                if (player.field_Private_APIUser_0 != null)
                {
                    if (player.field_Private_APIUser_0.id.Equals(userID))
                        return player;
                }
            }
            return null;
        }

        bool ValidatePlayerAvatar(Player player)
        {
            return !(player == null ||
                     player.field_Internal_VRCPlayer_0 == null ||
                     player.field_Internal_VRCPlayer_0.isActiveAndEnabled == false ||
                     player.field_Internal_VRCPlayer_0.field_Internal_Animator_0 == null ||
                     player.field_Internal_VRCPlayer_0.field_Internal_GameObject_0 == null ||
                     player.field_Internal_VRCPlayer_0.field_Internal_GameObject_0.name.IndexOf("Avatar_Utility_Base_") == 0);
        }

    }
}
