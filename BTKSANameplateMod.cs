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
        public const string Version = "2.1.0"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        public static BTKSANameplateMod instance;

        public static PlayerNameplate lastNameplate;

        public HarmonyInstance harmony;

        public bool isInit = false;

        private string settingsCategory = "BTKSANameplateFix";
        private string hiddenCustomSetting = "enableHiddenCustomNameplates";
        private string hideFriendsNameplates = "hideFriendsNameplates";
        private string trustColourMode = "trustColourMode";

        //Save prefs copy to compare for ReloadAllAvatars
        bool hiddenCustomLocal = false;
        bool hideFriendsLocal = false;
        string trustColourModeLocal = "off";

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
            MelonPrefs.RegisterString(settingsCategory, trustColourMode, "friends", "Trust Colour Mode");
            ExpansionKitApi.RegisterSettingAsStringEnum(settingsCategory, trustColourMode, new[] { ("off", "Disable Trust Colours"), ("friends", "Trust Colours (with friend colour)"), ("trustonly", "Trust Colours (Ignore friend colour)") });

            //Register our menu button
            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.UserQuickMenu).AddSimpleButton("Toggle Nameplate Visibility", ToggleNameplateVisiblity);

            //Initalize Harmony
            harmony = HarmonyInstance.Create("BTKStandalone");

            harmony.Patch(typeof(VRCAvatarManager).GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance), null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnVRCAMAwake", BindingFlags.NonPublic | BindingFlags.Static)));

            harmony.Patch(typeof(PlayerNameplate).GetMethod("Method_Public_Void_0", BindingFlags.Public | BindingFlags.Instance), null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnRebuild", BindingFlags.NonPublic | BindingFlags.Static)));

            ClassInjector.RegisterTypeInIl2Cpp<NameplateHelper>();

            Log("Loading Nameplate Assets");
            loadAssets();

            Log("Loading HiddenNameplateUserIDs from file", true);
            LoadHiddenNameplateFromFile();
            //Load the settings to the local copy to compare with SettingsApplied
            getPrefsLocal();
        }


        public override void OnModSettingsApplied()
        {
            if (hiddenCustomLocal != MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting) || trustColourModeLocal != MelonPrefs.GetString(settingsCategory, trustColourMode) || hideFriendsLocal != MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates))
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

                    if (!trustColourModeLocal.Equals("off"))
                    {
                        APIUser apiUser = player.field_Private_APIUser_0;

                        Color trustColor;
                        Color? textColor = null;

                        if (trustColourModeLocal.Equals("friends"))
                        {
                            trustColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(apiUser);
                            textColor = Color.white;
                        }
                        else
                        {
                            //Setup fake user
                            APIUser fakeUser = apiUser.MemberwiseClone().Cast<APIUser>();

                            //Fake ID to not detect as a friend
                            fakeUser.id = "";

                            trustColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(fakeUser);
                        }

                        Log("Setting nameplate colour", true);

                        ApplyNameplateColour(nameplate, trustColor, trustColor, textColor);
                    }

                    if (hiddenCustomLocal)
                    {

                        Transform avatarRoot = player.field_Internal_VRCPlayer_0.field_Internal_GameObject_0.transform;
                        for (int i = 0; i < avatarRoot.childCount; i++)
                        {
                            GameObject child = avatarRoot.GetChild(i).gameObject;
                            if (!child.active && child.name.StartsWith("BTKNameplate:"))
                            {
                                string[] values = child.name.Replace("BTKNameplate:", "").Split(':');

                                if (values.Length > 0)
                                {
                                    //If we got here bgColor should exist
                                    Color? bgColor = GetColourFromHTMLCode(values[0]);
                                    Color? iconBGColor = null;
                                    Color? textColor = null;
                                    Color? textColorLerp = null;

                                    if (values.Length > 1)
                                        iconBGColor = GetColourFromHTMLCode(values[1]);

                                    if (values.Length > 2)
                                        textColor = GetColourFromHTMLCode(values[2]);

                                    if (values.Length > 3)
                                        textColorLerp = GetColourFromHTMLCode(values[3]);

                                    ApplyNameplateColour(nameplate, bgColor, iconBGColor, textColor, textColorLerp);
                                }

                                //TODO: Figure out icons and stuff
                                /*SkinnedMeshRenderer iconComponent = child.GetComponent<SkinnedMeshRenderer>();
                                if (iconComponent != null)
                                {
                                    Log("RendererFound");
                                    ApplyIcon(iconComponent.material.mainTexture, nameplate, player);
                                }*/

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

        private Color? GetColourFromHTMLCode(string colourCode)
        {
            Color32 colourTemp;
            if (ColorUtility.DoTryParseHtmlColor(colourCode.ToUpper().Trim(), out colourTemp))
            {
                return colourTemp;
            }
            else
            {
                Log($"Warning: An avatar had an invalid HTML colour code set! {colourCode}");
            }
            return null;
        }

        private void resetNameplate(PlayerNameplate nameplate)
        {
            nameplate.uiContents.transform.localScale = new Vector3(1, 1, 1);

            NameplateHelper helper = nameplate.gameObject.GetComponent<NameplateHelper>();
            if (helper != null)
            {
                helper.ResetNameplate();
            }

            ApplyNameplateColour(nameplate, Color.white, Color.white, null, null, true);
        }

        private void ApplyIcon(Texture texture, PlayerNameplate nameplate, Player player)
        {
            nameplate.uiIconBackground.enabled = true;
            nameplate.uiUserImage.enabled = true;
            nameplate.uiUserImageContainer.SetActive(true);

            nameplate.uiUserImage.texture = texture;
        }

        /// <summary>
        /// Sets the colours of a nameplate
        /// </summary>
        /// <param name="nameplate">Target nameplate</param>
        /// <param name="bgColor">Affects main nameplate background and the quick stats background</param>
        /// <param name="iconBGColor">Affects the icon background</param>
        /// <param name="textColor">Sets the player name text</param>
        /// <param name="textColorLerp">Sets NameplateHelper to do a fade between textColor and textColorLerp on the player name text</param>
        /// <param name="resetToDefaultMat">Resets the materials on the nameplate</param>
        private void ApplyNameplateColour(PlayerNameplate nameplate, Color? bgColor = null, Color? iconBGColor = null, Color? textColor = null, Color? textColorLerp = null, bool resetToDefaultMat = false)
        {
            Log("Apply colours", true);

            if (!resetToDefaultMat)
            {
                nameplate.uiNameBackground.material = npUIMaterial;
                nameplate.uiQuickStatsBackground.material = npUIMaterial;
                nameplate.uiIconBackground.material = npUIMaterial;
            }
            else
            {
                nameplate.uiNameBackground.material = null;
                nameplate.uiQuickStatsBackground.material = null;
                nameplate.uiIconBackground.material = null;
            }

            NameplateHelper helper = nameplate.gameObject.GetComponent<NameplateHelper>();
            if (helper == null)
            {
                //Create helper component
                helper = nameplate.gameObject.AddComponent<NameplateHelper>();
                helper.SetNameplate(nameplate);
                Log("Created NameplateHelper on nameplate", true);
            }

            Color oldBGColor = nameplate.uiNameBackground.color;
            Color oldIconBGColor = nameplate.uiIconBackground.color;
            Color oldQSBGColor = nameplate.uiQuickStatsBackground.color;
            Color oldTextColor = nameplate.uiName.faceColor;

            //Are we setting BGColor?
            if (bgColor.HasValue)
            {
                Color bgColor2 = bgColor.Value;
                Color quickStatsBGColor = bgColor.Value;
                bgColor2.a = oldBGColor.a;
                quickStatsBGColor.a = oldQSBGColor.a;
                nameplate.uiNameBackground.color = bgColor2;
                nameplate.uiQuickStatsBackground.color = quickStatsBGColor;
            }

            //Are we setting an iconBGColor?
            if (iconBGColor.HasValue)
            {
                Color iconBGColor2 = bgColor.Value;
                iconBGColor2.a = oldIconBGColor.a;
                nameplate.uiIconBackground.color = iconBGColor2;
            }

            //Check if we should set the text colour
            if (textColor.HasValue && !textColorLerp.HasValue)
            {
                Color textColor2 = textColor.Value;

                textColor2.a = oldTextColor.a;

                helper.SetNameColour(textColor2);
                helper.OnRebuild();
            }

            //Check if we should be doing a colour lerp
            if(textColor.HasValue && textColorLerp.HasValue)
            {
                Color textColor2 = textColor.Value;
                Color textColorLerp2 = textColorLerp.Value;

                textColor2.a = oldTextColor.a;
                textColorLerp2.a = oldTextColor.a;

                helper.SetColourLerp(textColor2, textColorLerp2);
            }
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
            trustColourModeLocal = MelonPrefs.GetString(settingsCategory, trustColourMode);
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

        private static void OnRebuild(PlayerNameplate __instance)
        {
            NameplateHelper helper = __instance.gameObject.GetComponent<NameplateHelper>();
            if (helper != null)
            {
                helper.OnRebuild();
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
