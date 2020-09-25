using Harmony;
using MelonLoader;
using System;
using System.IO;
using System.Reflection;
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
        public const string Version = "1.2.1"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        public static BTKSANameplateMod instance;

        public HarmonyInstance harmony;

        public float nameplateDefaultSize = 0.0015f;
        public float customNameplateDefaultSize = 1.0f;

        private string settingsCategory = "BTKSANameplateFix";
        private string hiddenCustomSetting = "enableHiddenCustomNameplates";
        private string hideFriendsNameplates = "hideFriendsNameplates";
        private string hideNameplateBorder = "hideNameplateBorder";
        private string nameplateScaleSetting = "nameplateScale";
        private string dynamicResizerSetting = "dynamicResizer";
        private string dynamicResizerDistance = "dynamicResizerDist";

        //Helper PropertyInfo
        PropertyInfo avatarDescriptProperty;

        //Save prefs copy to compare for ReloadAllAvatars
        bool hiddenCustomLocal = false;
        bool hideFriendsLocal = false;
        bool hideNameplateLocal = false;
        int scaleLocal = 100;
        bool dynamicResizerLocal = false;
        float dynamicResDistLocal = 3f;

        //Assets
        AssetBundle shaderBundle;
        Shader borderShader;
        Shader tagShader;

        public override void VRChat_OnUiManagerInit()
        {
            Log("BTK Standalone: Nameplate Fix - Starting up");

            instance = this;

            if (Directory.Exists("BTKCompanion"))
            {
                Log("Woah, hold on a sec, it seems you might be running BTKCompanion, if this is true NameplateFix is built into that, and you should not be using this!");
                Log("If you are not currently using BTKCompanion please remove the BTKCompanion folder from your VRChat installation!");
                MelonLogger.LogError("Nameplate Fix has not started up! (BTKCompanion Exists)");
                return;
            }

            MelonPrefs.RegisterCategory(settingsCategory, "Nameplate Mod");
            MelonPrefs.RegisterBool(settingsCategory, hiddenCustomSetting, false, "Enable Hidden Custom Nameplates");
            MelonPrefs.RegisterBool(settingsCategory, hideFriendsNameplates, false, "Hide Friends Nameplates");
            MelonPrefs.RegisterBool(settingsCategory, hideNameplateBorder, false, "Hide Nameplate Borders");
            MelonPrefs.RegisterInt(settingsCategory, nameplateScaleSetting, 100, "Nameplate Size Percentage");
            MelonPrefs.RegisterBool(settingsCategory, dynamicResizerSetting, false, "Enable Dynamic Nameplate Resizer");
            MelonPrefs.RegisterFloat(settingsCategory, dynamicResizerDistance, 3f, "Dynamic Resizer Max Distance");

            //Register dynamic scaler
            ClassInjector.RegisterTypeInIl2Cpp<DynamicScaler>();
            ClassInjector.RegisterTypeInIl2Cpp<DynamicScalerCustom>();

            //Initalize Harmony
            harmony = HarmonyInstance.Create("BTKStandalone");
            harmony.Patch(typeof(VRCAvatarManager).GetMethod("Method_Private_Boolean_GameObject_String_Single_PDM_0", BindingFlags.Instance | BindingFlags.Public), null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnAvatarInit", BindingFlags.NonPublic | BindingFlags.Static)));

            avatarDescriptProperty = typeof(VRCAvatarManager).GetProperty("prop_VRC_AvatarDescriptor_0", BindingFlags.Public | BindingFlags.Instance, null, typeof(VRC_AvatarDescriptor), new Type[0], null);

            Log("Loading Assets from Embedded Bundle...");
            loadAssets();

            //Load the settings to the local copy to compare with SettingsApplied
            getPrefsLocal();
        }

        public override void OnModSettingsApplied()
        {
            if (hiddenCustomLocal != MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting) || hideFriendsLocal != MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates) || scaleLocal != MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting) || dynamicResizerLocal != MelonPrefs.GetBool(settingsCategory, dynamicResizerSetting) || dynamicResDistLocal != MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance) || hideNameplateLocal != MelonPrefs.GetBool(settingsCategory, hideNameplateBorder))
                VRCPlayer.field_Internal_Static_VRCPlayer_0.Method_Public_Void_Boolean_0();

            getPrefsLocal();
        }

        public void OnUpdatePlayer(Player player)
        {


            if (ValidatePlayerAvatar(player))
            {
                GDBUser user = new GDBUser(player, player.field_Private_APIUser_0.id);
                bool friend = false;
                if (!player.name.Contains("Local"))
                {
                    GameObject nameplate = user.vrcPlayer.field_Internal_VRCPlayer_0.field_Private_VRCWorldPlayerUiProfile_0.gameObject;

                    ////
                    /// Nameplate RNG Fix
                    ////

                    //User is remote, apply fix
                    Log($"New user or avatar change! Applying NameplateMod on { user.displayName }", true);
                    Vector3 npPos = nameplate.transform.position;
                    object avatarDescriptor = avatarDescriptProperty.GetValue(user.vrcPlayer.prop_VRCAvatarManager_0);
                    float viewPointY = 0;

                    //Get viewpoint for AV2 avatar
                    if (avatarDescriptor != null)
                        viewPointY = ((VRC_AvatarDescriptor)avatarDescriptor).ViewPosition.y;

                    //Get viewpoint for AV3 avatar
                    if (user.vrcPlayer.prop_VRCAvatarManager_0.prop_VRCAvatarDescriptor_0 != null)
                        viewPointY = user.vrcPlayer.prop_VRCAvatarManager_0.prop_VRCAvatarDescriptor_0.ViewPosition.y;

                    if (viewPointY > 0)
                    {
                        npPos.y = viewPointY + user.vrcPlayer.transform.position.y + 0.5f;
                        nameplate.transform.position = npPos;
                    }

                    ////
                    /// Player nameplate checks
                    ////

                    float nameplateScale = (MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting) / 100f) * 0.0015f;

                    //Reset Nameplate to default state and remove DynamicNameplateScalers
                    resetNameplate(nameplate, nameplateDefaultSize);

                    //Disable nameplates on friends
                    if (MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates))
                    {

                        if (APIUser.IsFriendsWith(user.vrcPlayer.field_Private_APIUser_0.id))
                        {
                            Vector3 newScale = new Vector3(0, 0, 0);
                            nameplate.transform.localScale = newScale;
                            friend = true;
                        }
                    }

                    //Setup static or dynamic scale
                    applyScale(user, nameplate, nameplateScale, nameplateDefaultSize, friend);


                    ////
                    /// Custom Nameplate Checks
                    ////

                    //Grab custom nameplate object for next 2 checks
                    Transform customNameplateObject = user.avatarObject.transform.Find("Custom Nameplate");
                    float customNameplateScale = (MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting) / 100f) * 1.0f;

                    //Reset Custom Nameplate scale
                    if (customNameplateObject != null)
                        resetNameplate(customNameplateObject.gameObject, customNameplateDefaultSize);

                    //Enable Hidden Custom Nameplate
                    if (MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting) && !(MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates) && friend))
                    {

                        if (customNameplateObject != null && !customNameplateObject.gameObject.active)
                        {
                            Log($"Found hidden Custom Nameplate on { user.displayName }, enabling.", true);
                            customNameplateObject.gameObject.SetActive(true);
                        }
                    }

                    //Check if nameplate should be hidden or resized
                    if (customNameplateObject != null && customNameplateObject.gameObject.active)
                    {
                        if (MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates) && friend)
                            customNameplateObject.gameObject.SetActive(false);

                        Transform tagAndBGObj = customNameplateObject.Find("Tag and Background");
                        Transform borderObj = customNameplateObject.Find("Border");

                        if (tagAndBGObj != null && borderObj != null)
                        {
                            SkinnedMeshRenderer tagRenderer = tagAndBGObj.gameObject.GetComponent<SkinnedMeshRenderer>();
                            SkinnedMeshRenderer borderRenderer = borderObj.gameObject.GetComponent<SkinnedMeshRenderer>();

                            //Replace shaders!
                            replaceCustomNameplateShader(tagRenderer, borderRenderer);

                            //Apply scaler
                            applyScale(user, customNameplateObject.gameObject, customNameplateScale, customNameplateDefaultSize, friend, true, tagRenderer, borderRenderer);
                        }
                    }

                    ////
                    /// Nameplate Misc Mods
                    ////

                    if (MelonPrefs.GetBool(settingsCategory, hideNameplateBorder))
                    {
                        Transform border = nameplate.transform.Find("Frames");
                        if (border != null)
                        {
                            border.gameObject.active = false;
                        }
                    }

                }
            }
        }

        /// <summary>
        /// Applies either static scale or DynamicScaler to target object
        /// </summary>
        /// <param name="user">Target player</param>
        /// <param name="target">Target GameObject</param>
        /// <param name="nameplateScale">New scale or target min scale</param>
        /// <param name="defaultSize">Default size of the target GameObject</param>
        /// <param name="isFriend"></param>
        private void applyScale(GDBUser user, GameObject target, float nameplateScale, float defaultSize, bool isFriend, bool isCustomNameplate = false, SkinnedMeshRenderer tagRenderer = null, SkinnedMeshRenderer borderRenderer = null)
        {
            if (nameplateScale != nameplateDefaultSize && !(isFriend && MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates)))
            {
                if (MelonPrefs.GetBool(settingsCategory, dynamicResizerSetting))
                {
                    if (!isCustomNameplate)
                    {
                        DynamicScaler component = target.AddComponent<DynamicScaler>();
                        component.ApplySettings(user.vrcPlayer, target, nameplateScale, defaultSize, MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance));
                    }
                    else
                    {
                        DynamicScalerCustom component = target.AddComponent<DynamicScalerCustom>();
                        if (tagRenderer != null && borderRenderer!=null)
                        {
                            component.ApplySettings(user.vrcPlayer, borderRenderer.material, tagRenderer.material, nameplateScale, defaultSize, MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance));
                        }
                    }
                }
                else
                {
                    if (!isCustomNameplate)
                    {
                        Vector3 newScale = new Vector3(nameplateScale, nameplateScale, nameplateScale);
                        target.transform.localScale = newScale;
                    }
                    else
                    {
                        if (tagRenderer != null)
                        {
                            tagRenderer.material.shader = tagShader;
                            tagRenderer.material.SetFloat("_Scale", nameplateScale);
                        }

                        if (borderRenderer != null)
                        {
                            borderRenderer.material.shader = borderShader;
                            borderRenderer.material.SetFloat("_Scale", nameplateScale);
                        }
                    }
                }
            }
        }

        private void replaceCustomNameplateShader(SkinnedMeshRenderer tagRenderer, SkinnedMeshRenderer borderRenderer)
        {
            if (tagRenderer != null)
            {
                tagRenderer.material.shader = tagShader;
                tagRenderer.material.SetFloat("_Scale", 1.0f);
            }

            if (borderRenderer != null)
            {
                borderRenderer.material.shader = borderShader;
                borderRenderer.material.SetFloat("_Scale", 1.0f);
            }
        }

        private void resetNameplate(GameObject nameplate, float defaultSize)
        {
            foreach (DynamicScaler scaler in nameplate.GetComponents<DynamicScaler>())
            {
                GameObject.Destroy(scaler);
            }

            nameplate.transform.localScale = new Vector3(defaultSize, defaultSize, defaultSize);

            //Reset Border Disable
            Transform border = nameplate.transform.Find("Frames");
            if (border != null)
            {
                border.gameObject.active = true;
            }
        }

        private void loadAssets()
        {
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSANameplateMod.assets"))
            {
                Log("Loaded Embedded resource", true);
                using (var tempStream = new MemoryStream((int)assetStream.Length))
                {
                    assetStream.CopyTo(tempStream);

                    shaderBundle = AssetBundle.LoadFromMemory_Internal(tempStream.ToArray(), 0);
                    shaderBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }

            if (shaderBundle != null)
            {
                borderShader = shaderBundle.LoadAsset_Internal("CustomBorder", Il2CppType.Of<Shader>()).Cast<Shader>();
                borderShader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                tagShader = shaderBundle.LoadAsset_Internal("CustomTag", Il2CppType.Of<Shader>()).Cast<Shader>();
                tagShader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            Log("Loaded Assets Successfully!", true);

        }

        private void getPrefsLocal()
        {
            hiddenCustomLocal = MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting);
            hideFriendsLocal = MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates);
            scaleLocal = MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting);
            dynamicResizerLocal = MelonPrefs.GetBool(settingsCategory, dynamicResizerSetting);
            dynamicResDistLocal = MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance);
            hideNameplateLocal = MelonPrefs.GetBool(settingsCategory, hideNameplateBorder);
        }

        private static void OnAvatarInit(GameObject __0, ref VRCAvatarManager __instance, ref bool __result)
        {
            if (__instance.field_Private_VRCPlayer_0.field_Private_Player_0 != null)
            {
                if (__instance.field_Private_VRCPlayer_0.field_Private_Player_0.field_Private_APIUser_0 != null)
                {
                    //User changed avatar, send to GDBManager for processing
                    BTKSANameplateMod.instance.OnUpdatePlayer(__instance.field_Private_VRCPlayer_0.field_Private_Player_0);
                }
            }

        }

        public static void Log(string log, bool dbg = false)
        {
            if (!Imports.IsDebugMode() && dbg)
                return;

            MelonLogger.Log(log);
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
