using Harmony;
using Il2CppSystem.Text;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TMPro;
using UIExpansionKit.API;
using UnhollowerRuntimeLib;
using UnhollowerRuntimeLib.XrefScans;
using UnityEngine;
using UnityEngine.UI;
using VRC;
using VRC.Core;

namespace BTKSANameplateMod
{
    public static class BuildInfo
    {
        public const string Name = "BTKSANameplateMod"; // Name of the Mod.  (MUST BE SET)
        public const string Author = "DDAkebono#0001"; // Author of the Mod.  (Set as null if none)
        public const string Company = "BTK-Development"; // Company that made the Mod.  (Set as null if none)
        public const string Version = "2.3.1"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        #region Variables

        public static BTKSANameplateMod instance;

        public static bool IgnoreFriends = false;
        public static bool IsQMOpen = false;

        public HarmonyInstance harmony;

        private string settingsCategory = "BTKSANameplateFix";
        private string hiddenCustomSetting = "enableHiddenCustomNameplates";
        private string hideFriendsNameplates = "hideFriendsNameplates";
        private string trustColourMode = "trustColourMode";
        private string nameplateOutlineMode = "nameplateOutline";
        private string nameplateAlwaysShowQuickStats = "nmAlwaysShowQuickInfo";
        private string nameplateCloseRangeFade = "nmCloseRangeFade";
        private string nameplateCloseRangeDistMin = "nmCloseRangeDistMin";
        private string nameplateCloseRangeDistMax = "nmCloseRangeDistMax";
        private string nameplateRandomColours = "nmRandomColours";

        private Regex methodMatchRegex = new Regex("Method_Public_Void_\\d", RegexOptions.Compiled);

        //Save prefs copy to compare for ReloadAllAvatars
        bool hiddenCustomLocal = false;
        bool hideFriendsLocal = false;
        string trustColourModeLocal = "off";
        bool nameplateOutlineModeLocal = false;
        bool alwaysShowStatsLocal = false;
        bool closeRangeFadeLocal = false;
        float closeRangeDistMin = 2f;
        float closeRangeDistMax = 3f;
        bool randomColourLocal = false;

        Sprite nameplateBGBackup;

        AssetBundle bundle;
        Material npUIMaterial;
        Sprite nameplateOutline;

        List<string> hiddenNameplateUserIDs = new List<string>();

        MethodInfo reloadAvatarsMethod;

        MD5 hashing = MD5.Create();

        #endregion

        public override void VRChat_OnUiManagerInit()
        {
            Log("BTK Standalone: Nameplate Mod - Starting up");

            instance = this;

            if (MelonHandler.Mods.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                MelonLogger.Msg("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                MelonLogger.Error("BTKSANameplateMod has not started up! (BTKCompanion Running)");
                return;
            }

            MelonPreferences.CreateCategory(settingsCategory, "Nameplate Mod");
            MelonPreferences.CreateEntry<bool>(settingsCategory, hiddenCustomSetting, false, "Enable Custom Nameplates (Not ready)");
            MelonPreferences.CreateEntry<bool>(settingsCategory, hideFriendsNameplates, false, "Hide Friends Nameplates");
            MelonPreferences.CreateEntry<string>(settingsCategory, trustColourMode, "friends", "Trust Colour Mode");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateOutlineMode, false, "Nameplate Outline Background");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateAlwaysShowQuickStats, false, "Always Show Quick Stats");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateCloseRangeFade, false, "Close Range Fade");
            MelonPreferences.CreateEntry<float>(settingsCategory, nameplateCloseRangeDistMin, 2f, "Close Range Min Distance");
            MelonPreferences.CreateEntry<float>(settingsCategory, nameplateCloseRangeDistMax, 3f, "Close Range Max Distance");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateRandomColours, false, "Random Nameplate Colours");
            ExpansionKitApi.RegisterSettingAsStringEnum(settingsCategory, trustColourMode, new[] { ("off", "Disable Trust Colours"), ("friends", "Show Friends Colour"), ("trustonly", "Ignore Friends Colour"), ("trustname", "Trust Colour On Name") });

            //Register our menu button
            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.UserQuickMenu).AddSimpleButton("Toggle Nameplate Visibility", ToggleNameplateVisiblity);

            //Initalize Harmony
            harmony = HarmonyInstance.Create("BTKStandalone");

            harmony.Patch(typeof(VRCPlayer).GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance), null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnVRCPlayerAwake", BindingFlags.NonPublic | BindingFlags.Static)));

            foreach (MethodInfo method in typeof(PlayerNameplate).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => methodMatchRegex.IsMatch(x.Name)))
            {
                Log($"Found target Rebuild method ({method.Name})", true);
                harmony.Patch(method, null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnRebuild", BindingFlags.NonPublic | BindingFlags.Static)));
            }

            //Loukylor is a fuckin nerd, also thanks for the things, very good shit
            harmony.Patch(typeof(APIUser).GetMethod("IsFriendsWith", BindingFlags.Public | BindingFlags.Static), new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("FriendsPatch", BindingFlags.Public | BindingFlags.Static)));

            Log("Patching QM Open/Close functions", true);
            MethodInfo closeQuickMenu = typeof(QuickMenu).GetMethods()
                .Where(mb => mb.Name.StartsWith("Method_Public_Void_Boolean_") && mb.Name.Length <= 29 && !mb.Name.Contains("PDM") && CheckUsed(mb, "Method_Public_Void_Int32_Boolean_")).First();

            MethodInfo openQuickMenu = typeof(QuickMenu).GetMethods()
                 .Where(mb => mb.Name.StartsWith("Method_Public_Void_Boolean_") && mb.Name.Length <= 29 && mb.GetParameters().Any(pi => pi.HasDefaultValue == false)).First();

            try
            {
                harmony.Patch(openQuickMenu, null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("QMOpen", BindingFlags.Public | BindingFlags.Static)));
                harmony.Patch(closeQuickMenu, null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("QMClose", BindingFlags.Public | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                Log("Unable to patch Quickmenu Open/Close functions!");
            }

            reloadAvatarsMethod = typeof(VRCPlayer).GetMethods().Where(method => method.Name.Contains("Method_Public_Void_Boolean_") && method.GetParameters().Any(param => param.IsOptional)).First();
            if (reloadAvatarsMethod == null)
                Log("Unable to get Reload All Avatars method!");

            ClassInjector.RegisterTypeInIl2Cpp<NameplateHelper>();

            Log("Loading Nameplate Assets");
            loadAssets();

            Log("Loading HiddenNameplateUserIDs from file", true);
            LoadHiddenNameplateFromFile();
            //Load the settings to the local copy to compare with SettingsApplied
            getPrefsLocal();
        }

        public override void OnPreferencesSaved()
        {
            if (getPrefsLocal()) 
            {
                reloadAvatarsMethod.Invoke(VRCPlayer.field_Internal_Static_VRCPlayer_0, new object[] { false });
            }
        }

        public void OnAvatarIsReady(VRCPlayer vrcPlayer)
        {
            if (ValidatePlayerAvatar(vrcPlayer))
            {
                Player player = vrcPlayer._player;

                if (vrcPlayer.field_Public_PlayerNameplate_0 == null)
                    return;

                PlayerNameplate nameplate = vrcPlayer.field_Public_PlayerNameplate_0;
                NameplateHelper helper = nameplate.GetComponent<NameplateHelper>();
                if (helper == null)
                {
                    helper = nameplate.gameObject.AddComponent<NameplateHelper>();
                    helper.SetNameplate(nameplate);
                    Log("Fetching objects from hierarhcy", true);
                    helper.uiQuickStatsGO = nameplate.gameObject.transform.Find("Contents/Quick Stats").gameObject;
                    helper.uiIconBackground = nameplate.gameObject.transform.Find("Contents/Icon/Background").GetComponent<Image>();
                    helper.uiUserImage = nameplate.gameObject.transform.Find("Contents/Icon/User Image").GetComponent<RawImage>();
                    helper.uiUserImageContainer = nameplate.gameObject.transform.Find("Contents/Icon").gameObject;
                    helper.uiNameBackground = nameplate.gameObject.transform.Find("Contents/Main/Background").GetComponent<ImageThreeSlice>();
                    helper.uiQuickStatsBackground = nameplate.gameObject.transform.Find("Contents/Quick Stats").GetComponent<ImageThreeSlice>();
                    helper.uiName = nameplate.gameObject.transform.Find("Contents/Main/Text Container/Name").GetComponent<TextMeshProUGUI>();
                    Log("Created NameplateHelper on nameplate", true);
                }

                resetNameplate(nameplate);

                ////
                /// Player nameplate checks
                ////
                ///

                //Check if we should be showing quick stats
                helper.AlwaysShowQuickInfo = alwaysShowStatsLocal;

                //Enable close range fade
                if (!vrcPlayer.name.Contains("Local") && closeRangeFadeLocal)
                {
                    helper.uiGroup = vrcPlayer.field_Public_PlayerNameplate_0.gameObject.GetComponent<CanvasGroup>();
                    helper.localPlayerGO = VRCVrCamera.field_Private_Static_VRCVrCamera_0.gameObject;
                    helper.closeRangeFade = true;
                    helper.fadeMinRange = closeRangeDistMin;
                    helper.fadeMaxRange = closeRangeDistMax;
                }

                //Check if we should replace the background with outline
                if (nameplateOutlineModeLocal)
                {
                    ImageThreeSlice bgImage = helper.uiNameBackground.GetComponent<ImageThreeSlice>();
                    if (bgImage != null)
                    {
                        if (nameplateBGBackup == null)
                            nameplateBGBackup = bgImage._sprite;

                        bgImage._sprite = nameplateOutline;
                    }
                }

                //Check if the Nameplate should be hidden
                if (hiddenNameplateUserIDs.Contains(player.field_Private_APIUser_0.id))
                {
                    Log("Hiding nameplate - HiddenSet", true);
                    helper.gameObject.transform.localScale = Vector3.zero;
                    return;
                }

                //Trust colour replacer
                if (!trustColourModeLocal.Equals("off") && !randomColourLocal)
                {
                    APIUser apiUser = player.field_Private_APIUser_0;

                    Color? trustColor = null;
                    Color? textColor = null;
                    bool resetMaterials = false;

                    if (trustColourModeLocal.Equals("friends"))
                    {
                        trustColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(apiUser);
                        textColor = UnityEngine.Color.white;
                    }

                    if (trustColourModeLocal.Equals("trustonly"))
                    {
                        BTKSANameplateMod.IgnoreFriends = true;
                        trustColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(apiUser);
                        BTKSANameplateMod.IgnoreFriends = false;
                    }

                    if (trustColourModeLocal.Equals("trustname"))
                    {
                        textColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(apiUser);
                        resetMaterials = true;
                    }

                    Log("Setting nameplate colour", true);

                    ApplyNameplateColour(nameplate, helper, trustColor, trustColor, textColor, null, resetMaterials);
                }

                if (randomColourLocal)
                {
                    var hash = hashing.ComputeHash(Encoding.UTF8.GetBytes(player.field_Private_APIUser_0.id));
                    Color nameplateColour = new Color(hash[1] / 255f, hash[2] / 255f, hash[3] / 255f);

                    ApplyNameplateColour(nameplate, helper, nameplateColour, nameplateColour, null, null, false);
                }

                //Jank custom colour system that isn't done
                if (hiddenCustomLocal)
                {

                    Transform avatarRoot = vrcPlayer.field_Internal_GameObject_0.transform;
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

                                ApplyNameplateColour(nameplate, helper, bgColor, iconBGColor, textColor, textColorLerp);
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
                            helper.gameObject.transform.localScale = Vector3.zero;
                        }
                    }
                }

                helper.OnRebuild(IsQMOpen);
            }
        }

        #region Nameplate Functions

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
            nameplate.gameObject.transform.localScale = new Vector3(1, 1, 1);

            NameplateHelper helper = nameplate.gameObject.GetComponent<NameplateHelper>();
            if (helper != null)
            {
                helper.ResetNameplate();
            }

            //Outline mode was enabled at some point so let's make sure to reset it
            if (nameplateBGBackup != null && helper != null)
            {
                ImageThreeSlice bgImage = helper.uiNameBackground.GetComponent<ImageThreeSlice>();
                if (bgImage != null)
                {
                    bgImage._sprite = nameplateBGBackup;
                }
            }

            ApplyNameplateColour(nameplate, helper, UnityEngine.Color.white, UnityEngine.Color.white, null, null, true);
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
        private void ApplyNameplateColour(PlayerNameplate nameplate, NameplateHelper helper, Color? bgColor = null, Color? iconBGColor = null, Color? textColor = null, Color? textColorLerp = null, bool resetToDefaultMat = false)
        {
            if (helper == null)
                return;

            Log("Apply colours", true);

            if (!resetToDefaultMat)
            {
                helper.uiNameBackground.material = npUIMaterial;
                helper.uiQuickStatsBackground.material = npUIMaterial;
                helper.uiIconBackground.material = npUIMaterial;
            }
            else
            {
                helper.uiNameBackground.material = null;
                helper.uiQuickStatsBackground.material = null;
                helper.uiIconBackground.material = null;
            }

            Color oldBGColor = helper.uiNameBackground.color;
            Color oldIconBGColor = helper.uiIconBackground.color;
            Color oldQSBGColor = helper.uiQuickStatsBackground.color;
            Color oldTextColor = helper.uiName.faceColor;

            //Are we setting BGColor?
            if (bgColor.HasValue)
            {
                Color bgColor2 = bgColor.Value;
                Color quickStatsBGColor = bgColor.Value;
                bgColor2.a = oldBGColor.a;
                quickStatsBGColor.a = oldQSBGColor.a;
                helper.uiNameBackground.color = bgColor2;
                helper.uiQuickStatsBackground.color = quickStatsBGColor;
            }

            //Are we setting an iconBGColor?
            if (iconBGColor.HasValue)
            {
                Color iconBGColor2 = bgColor.Value;
                iconBGColor2.a = oldIconBGColor.a;
                helper.uiIconBackground.color = iconBGColor2;
            }

            //Check if we should set the text colour
            if (textColor.HasValue && !textColorLerp.HasValue)
            {
                Color textColor2 = textColor.Value;

                textColor2.a = oldTextColor.a;

                helper.SetNameColour(textColor2);
                helper.OnRebuild(IsQMOpen);
            }

            //Check if we should be doing a colour lerp
            if (textColor.HasValue && textColorLerp.HasValue)
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
            OnAvatarIsReady(QuickMenu.prop_QuickMenu_0.field_Private_Player_0._vrcplayer);
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

        private void ApplyIcon(Texture texture, PlayerNameplate nameplate, NameplateHelper helper, Player player)
        {
            helper.uiIconBackground.enabled = true;
            helper.uiUserImage.enabled = true;
            helper.uiUserImageContainer.SetActive(true);

            helper.uiUserImage.texture = texture;
        }

        #endregion

        private bool getPrefsLocal()
        {
            bool updated = false;

            if (hiddenCustomLocal != MelonPreferences.GetEntryValue<bool>(settingsCategory, hiddenCustomSetting))
            {
                hiddenCustomLocal = MelonPreferences.GetEntryValue<bool>(settingsCategory, hiddenCustomSetting);
                updated = true;
            }

            if (hideFriendsLocal != MelonPreferences.GetEntryValue<bool>(settingsCategory, hideFriendsNameplates))
            {
                hideFriendsLocal = MelonPreferences.GetEntryValue<bool>(settingsCategory, hideFriendsNameplates);
                updated = true;
            }

            if (trustColourModeLocal != MelonPreferences.GetEntryValue<string>(settingsCategory, trustColourMode))
            {
                trustColourModeLocal = MelonPreferences.GetEntryValue<string>(settingsCategory, trustColourMode);
                updated = true;
            }

            if (nameplateOutlineModeLocal != MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateOutlineMode))
            {
                nameplateOutlineModeLocal = MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateOutlineMode);
                updated = true;
            }

            if (alwaysShowStatsLocal != MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateAlwaysShowQuickStats))
            {
                alwaysShowStatsLocal = MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateAlwaysShowQuickStats); ;
                updated = true;
            }

            if (closeRangeFadeLocal != MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateCloseRangeFade))
            {
                closeRangeFadeLocal = MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateCloseRangeFade);
                updated = true;
            }

            if (closeRangeDistMin != MelonPreferences.GetEntryValue<float>(settingsCategory, nameplateCloseRangeDistMin))
            {
                closeRangeDistMin = MelonPreferences.GetEntryValue<float>(settingsCategory, nameplateCloseRangeDistMin);
                updated = true;
            }

            if (closeRangeDistMax != MelonPreferences.GetEntryValue<float>(settingsCategory, nameplateCloseRangeDistMax))
            {
                closeRangeDistMax = MelonPreferences.GetEntryValue<float>(settingsCategory, nameplateCloseRangeDistMax);
                updated = true;
            }

            if (randomColourLocal != MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateRandomColours))
            {
                randomColourLocal = MelonPreferences.GetEntryValue<bool>(settingsCategory, nameplateRandomColours);
                updated = true;
            }

            return updated;
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
                nameplateOutline = bundle.LoadAsset_Internal("NameplateOutline", Il2CppType.Of<Sprite>()).Cast<Sprite>();
                nameplateOutline.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
        }

        #region Patches

        private static void OnRebuild(PlayerNameplate __instance)
        {
            NameplateHelper helper = __instance.gameObject.GetComponent<NameplateHelper>();
            if (helper != null)
            {
                helper.OnRebuild(IsQMOpen);
            }
            else
            {
                //Nameplate doesn't have a helper, lets fix that
                if (__instance.field_Private_VRCPlayer_0 != null)
                    if (__instance.field_Private_VRCPlayer_0._player != null && __instance.field_Private_VRCPlayer_0._player.field_Private_APIUser_0 != null)
                        instance.OnAvatarIsReady(__instance.field_Private_VRCPlayer_0);
            }
        }

        private static void OnVRCPlayerAwake(VRCPlayer __instance)
        {
            __instance.Method_Public_add_Void_MulticastDelegateNPublicSealedVoUnique_0(new Action(() => {
                if (__instance != null)
                {
                    if (__instance._player != null)
                        if (__instance._player.field_Private_APIUser_0 != null)
                            BTKSANameplateMod.instance.OnAvatarIsReady(__instance);
                }
            }));
        }

        public static bool FriendsPatch(ref bool __result)
        {
            if (IgnoreFriends)
            {
                __result = false;
                return false;
            }
            return true;
        }

        public static void QMOpen()
        {
            IsQMOpen = true;
        }

        public static void QMClose()
        {
            IsQMOpen = false;
        }

        #endregion

        #region Utils

        public static void Log(string log, bool dbg = false)
        {
            if (!MelonDebug.IsEnabled() && dbg)
                return;

            MelonLogger.Msg(log);
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

        bool ValidatePlayerAvatar(VRCPlayer player)
        {
            return !(player == null ||
                     player.isActiveAndEnabled == false ||
                     player.field_Internal_Animator_0 == null ||
                     player.field_Internal_GameObject_0 == null ||
                     player.field_Internal_GameObject_0.name.IndexOf("Avatar_Utility_Base_") == 0);
        }

        public static bool CheckUsed(MethodBase methodBase, string methodName)
        {
            try
            {
                return UnhollowerRuntimeLib.XrefScans.XrefScanner.UsedBy(methodBase).Where(instance => instance.TryResolve() != null && instance.TryResolve().Name.Contains(methodName)).Any();
            }
            catch { }
            return false;
        }

        #endregion
    }
}
