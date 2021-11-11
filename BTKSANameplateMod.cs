using Il2CppSystem.Text;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HarmonyLib;
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
        public const string Version = "2.4.1"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        #region Variables

        public static BTKSANameplateMod instance;

        public static bool IgnoreFriends = false;
        public static bool IsQMOpen = false;
        public static Regex methodMatchRegex = new Regex("Method_Public_Void_\\d", RegexOptions.Compiled);

        private readonly string settingsCategory = "BTKSANameplateFix";
        private readonly string hiddenCustomSetting = "enableHiddenCustomNameplates";
        private readonly string hideFriendsNameplates = "hideFriendsNameplates";
        private readonly string trustColourMode = "trustColourMode";
        private readonly string nameplateOutlineMode = "nameplateOutline";
        private readonly string nameplateAlwaysShowQuickStats = "nmAlwaysShowQuickInfo";
        private readonly string nameplateCloseRangeFade = "nmCloseRangeFade";
        private readonly string nameplateCloseRangeDistMin = "nmCloseRangeDistMin";
        private readonly string nameplateCloseRangeDistMax = "nmCloseRangeDistMax";
        private readonly string nameplateRandomColours = "nmRandomColours";
        private readonly string NameplateCloseRangeFadeFriends = "nmCloseRangeFadeFriends";

        //Save prefs copy to compare for ReloadAllAvatars
        private bool hiddenCustomLocal = false;
        private bool hideFriendsLocal = false;
        private string trustColourModeLocal = "off";
        private bool nameplateOutlineModeLocal = false;
        private bool alwaysShowStatsLocal = false;
        private bool closeRangeFadeLocal = false;
        private float closeRangeDistMin = 2f;
        private float closeRangeDistMax = 3f;
        private bool randomColourLocal = false;
        private bool closeRangeFadeFriendsOnly = false;
        private int scenesLoaded = 0;

        private Sprite nameplateBGBackup;

        private AssetBundle bundle;
        private Material npUIMaterial;
        private Sprite nameplateOutline;

        private List<string> hiddenNameplateUserIDs = new List<string>();

        private MethodInfo reloadAvatarsMethod;

        #endregion

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (scenesLoaded <= 2)
            {
                scenesLoaded++;
                if (scenesLoaded == 2)
                    UiManagerInit();
            }
        }

        public void UiManagerInit()
        {
            BTKUtil.Log("BTK Standalone: Nameplate Mod - Starting up");
            
            instance = this;

            if (MelonHandler.Mods.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                MelonLogger.Msg("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                MelonLogger.Error("BTKSANameplateMod has not started up! (BTKCompanion Running)");
                return;
            }

            BTKUtil.Log(settingsCategory);
            
            MelonPreferences.CreateCategory(settingsCategory, "Nameplate Mod");
            MelonPreferences.CreateEntry<bool>(settingsCategory, hiddenCustomSetting, false, "Enable Custom Nameplates (Not ready)");
            MelonPreferences.CreateEntry<bool>(settingsCategory, hideFriendsNameplates, false, "Hide Friends Nameplates");
            MelonPreferences.CreateEntry<string>(settingsCategory, trustColourMode, "friends", "Trust Colour Mode");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateOutlineMode, false, "Nameplate Outline Background");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateAlwaysShowQuickStats, false, "Always Show Quick Stats");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateCloseRangeFade, false, "Close Range Fade");
            MelonPreferences.CreateEntry<bool>(settingsCategory, NameplateCloseRangeFadeFriends, false, "Close Range Fade Friends Only");
            MelonPreferences.CreateEntry<float>(settingsCategory, nameplateCloseRangeDistMin, 2f, "Close Range Min Distance");
            MelonPreferences.CreateEntry<float>(settingsCategory, nameplateCloseRangeDistMax, 3f, "Close Range Max Distance");
            MelonPreferences.CreateEntry<bool>(settingsCategory, nameplateRandomColours, false, "Random Nameplate Colours");
            ExpansionKitApi.RegisterSettingAsStringEnum(settingsCategory, trustColourMode, new[] { ("off", "Disable Trust Colours"), ("friends", "Show Friends Colour"), ("trustonly", "Ignore Friends Colour"), ("trustname", "Trust Colour On Name") });
            
            //Apply patches
            applyPatches(typeof(QuickMenuPatches));
            applyPatches(typeof(NameplatePatches));
            applyPatches(typeof(ApiUserPatches));
            applyPatches(typeof(VRCPlayerPatches));

            //Register our menu button
            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.UserQuickMenu).AddSimpleButton("Toggle Nameplate Visibility", ToggleNameplateVisibility);

            reloadAvatarsMethod = typeof(VRCPlayer).GetMethods().First(method => method.Name.Contains("Method_Public_Void_Boolean_") && method.GetParameters().Any(param => param.IsOptional));
            if (reloadAvatarsMethod == null)
                BTKUtil.Log("Unable to get Reload All Avatars method!");

            ClassInjector.RegisterTypeInIl2Cpp<NameplateHelper>();

            BTKUtil.Log("Loading Nameplate Assets");
            loadAssets();

            BTKUtil.Log("Loading HiddenNameplateUserIDs from file", true);
            LoadHiddenNameplateFromFile();
            //Load the settings to the local copy to compare with SettingsApplied
            getPrefsLocal();
        }
        
        private void applyPatches(Type type)
        {
            try
            {
                HarmonyLib.Harmony.CreateAndPatchAll(type, "BTKHarmonyInstance");
            }
            catch(Exception e)
            {
                BTKUtil.Log($"Failed while patching {type.Name}!");
                MelonLogger.Error(e);
            }
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
            if (BTKUtil.ValidatePlayerAvatar(vrcPlayer))
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
                    BTKUtil.Log("Fetching objects from hierarhcy", true);
                    helper.uiQuickStatsGO = nameplate.gameObject.transform.Find("Contents/Quick Stats").gameObject;
                    helper.uiIconBackground = nameplate.gameObject.transform.Find("Contents/Icon/Background").GetComponent<Image>();
                    helper.uiUserImage = nameplate.gameObject.transform.Find("Contents/Icon/User Image").GetComponent<RawImage>();
                    helper.uiUserImageContainer = nameplate.gameObject.transform.Find("Contents/Icon").gameObject;
                    helper.uiNameBackground = nameplate.gameObject.transform.Find("Contents/Main/Background").GetComponent<ImageThreeSlice>();
                    helper.uiQuickStatsBackground = nameplate.gameObject.transform.Find("Contents/Quick Stats").GetComponent<ImageThreeSlice>();
                    helper.uiName = nameplate.gameObject.transform.Find("Contents/Main/Text Container/Name").GetComponent<TextMeshProUGUI>();
                    BTKUtil.Log("Created NameplateHelper on nameplate", true);
                }

                resetNameplate(nameplate);

                ////
                /// Player nameplate checks
                ////
                ///

                //Check if we should be showing quick stats
                helper.AlwaysShowQuickInfo = alwaysShowStatsLocal;

                //Enable close range fade
                if (!vrcPlayer.name.Contains("Local") && closeRangeFadeLocal && (!closeRangeFadeFriendsOnly || APIUser.IsFriendsWith(vrcPlayer._player.prop_APIUser_0.id)))
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
                if (hiddenNameplateUserIDs.Contains(player.prop_APIUser_0.id))
                {
                    BTKUtil.Log("Hiding nameplate - HiddenSet", true);
                    helper.gameObject.transform.localScale = Vector3.zero;
                    return;
                }

                //Trust colour replacer
                if (!trustColourModeLocal.Equals("off") && !randomColourLocal)
                {
                    APIUser apiUser = player.prop_APIUser_0;

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
                        IgnoreFriends = true;
                        trustColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(apiUser);
                        IgnoreFriends = false;
                    }

                    if (trustColourModeLocal.Equals("trustname"))
                    {
                        textColor = VRCPlayer.Method_Public_Static_Color_APIUser_0(apiUser);
                        resetMaterials = true;
                    }

                    BTKUtil.Log("Setting nameplate colour", true);

                    ApplyNameplateColour(nameplate, helper, trustColor, trustColor, textColor, null, resetMaterials);
                }

                if (randomColourLocal)
                {
                    Color nameplateColour = BTKUtil.GetColourFromUserID(player.prop_APIUser_0.id);

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
                    if (player.prop_APIUser_0 != null)
                    {
                        if (APIUser.IsFriendsWith(player.prop_APIUser_0.id))
                        {
                            BTKUtil.Log("Hiding Nameplate - FriendsHide", true);
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
                BTKUtil.Log($"Warning: An avatar had an invalid HTML colour code set! {colourCode}");
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

            BTKUtil.Log("Apply colours", true);

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

        private void ToggleNameplateVisibility()
        {
            APIUser user = BTKUtil.GetSelectedAPIUser();
            if (user == null) return;
            
            ToggleNameplateVisibility(user.id);
        }
        
        public void ToggleNameplateVisibility(string userid)
        {
            if (!hiddenNameplateUserIDs.Contains(userid))
                hiddenNameplateUserIDs.Add(userid);
            else
                hiddenNameplateUserIDs.Remove(userid);

            SaveHiddenNameplateFile();
            Player player = BTKUtil.getPlayerFromPlayerlist(userid);
            
            if(player!=null)
                OnAvatarIsReady(player._vrcplayer);
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

            if (closeRangeFadeFriendsOnly != MelonPreferences.GetEntryValue<bool>(settingsCategory, NameplateCloseRangeFadeFriends))
            {
                closeRangeFadeFriendsOnly = MelonPreferences.GetEntryValue<bool>(settingsCategory, NameplateCloseRangeFadeFriends);
                updated = true;
            }

            return updated;
        }

        private void loadAssets()
        {
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSANameplateMod.nmasset"))
            {
                BTKUtil.Log("Loaded Embedded resource");
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
    }
    
    [HarmonyPatch]
    class NameplatePatches 
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(PlayerNameplate).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => BTKSANameplateMod.methodMatchRegex.IsMatch(x.Name)).Cast<MethodBase>();
        }

        static void Postfix(PlayerNameplate __instance)
        {
            if (__instance == null || __instance.gameObject == null) return;
            
            NameplateHelper helper = __instance.gameObject.GetComponent<NameplateHelper>();
            if (helper != null)
            {
                helper.OnRebuild(BTKSANameplateMod.IsQMOpen);
            }
            else
            {
                //Nameplate doesn't have a helper, lets fix that
                if (__instance.field_Private_VRCPlayer_0 != null)
                    if (__instance.field_Private_VRCPlayer_0._player != null && __instance.field_Private_VRCPlayer_0._player.prop_APIUser_0 != null)
                        BTKSANameplateMod.instance.OnAvatarIsReady(__instance.field_Private_VRCPlayer_0);
            }
        }
    }
    
    [HarmonyPatch(typeof(VRC.UI.Elements.QuickMenu))]
    class QuickMenuPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnEnable")]
        private static void OnQuickMenuEnable()
        {
            BTKSANameplateMod.IsQMOpen = true;
        }
        
        [HarmonyPostfix]
        [HarmonyPatch("OnDisable")]
        private static void OnQuickMenuDisable()
        {
            BTKSANameplateMod.IsQMOpen = false;
        }
    }
    
    [HarmonyPatch(typeof(APIUser))]
    class ApiUserPatches 
    {
        [HarmonyPrefix]
        [HarmonyPatch("IsFriendsWith")]
        public static bool FriendsPatch(ref bool __result)
        {
            if (BTKSANameplateMod.IgnoreFriends)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
    
    [HarmonyPatch(typeof(VRCPlayer))]
    class VRCPlayerPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        private static void OnVRCPlayerAwake(VRCPlayer __instance)
        {
            __instance.Method_Public_add_Void_OnAvatarIsReady_0(new Action(() => {
                if (__instance != null)
                {
                    if (__instance._player != null)
                        if (__instance._player.prop_APIUser_0 != null)
                            BTKSANameplateMod.instance.OnAvatarIsReady(__instance);
                }
            }));
        }
    }
}
