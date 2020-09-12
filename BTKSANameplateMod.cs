using Harmony;
using MelonLoader;
using System;
using System.IO;
using System.Reflection;
using UnhollowerRuntimeLib;
using UnityEngine;
using VRC;
using VRC.SDKBase;

namespace BTKSANameplateMod
{
    public static class BuildInfo
    {
        public const string Name = "BTKSANameplateMod"; // Name of the Mod.  (MUST BE SET)
        public const string Author = "DDAkebono#0001"; // Author of the Mod.  (Set as null if none)
        public const string Company = "BTK-Development"; // Company that made the Mod.  (Set as null if none)
        public const string Version = "1.1.0"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateMod : MelonMod
    {
        public static BTKSANameplateMod instance;

        public HarmonyInstance harmony;

        public float nameplateDefaultSize = 0.0015f;

        private string settingsCategory = "BTKSANameplateFix";
        private string hiddenCustomSetting = "enableHiddenCustomNameplates";
        private string hideFriendsNameplates = "hideFriendsNameplates";
        private string nameplateScaleSetting = "nameplateScale";
        private string dynamicResizerSetting = "dynamicResizer";
        private string dynamicResizerDistance = "dynamicResizerDist";

        //Helper PropertyInfo
        PropertyInfo avatarDescriptProperty;

        //Save prefs copy to compare for ReloadAllAvatars
        bool hiddenCustomLocal = false;
        bool hideFriendsLocal = false;
        int scaleLocal = 100;
        bool dynamicResizerLocal = false;
        float dynamicResDistLocal = 3f;

        public override void VRChat_OnUiManagerInit()
        {
            MelonLogger.Log("BTK Standalone: Nameplate Fix - Starting up");

            instance = this;

            if (Directory.Exists("BTKCompanion"))
            {
                MelonLogger.Log("Woah, hold on a sec, it seems you might be running BTKCompanion, if this is true NameplateFix is built into that, and you should not be using this!");
                MelonLogger.Log("If you are not currently using BTKCompanion please remove the BTKCompanion folder from your VRChat installation!");
                MelonLogger.LogError("Nameplate Fix has not started up! (BTKCompanion Exists)");
                return;
            }

            MelonPrefs.RegisterCategory(settingsCategory, "Nameplate Mod");
            MelonPrefs.RegisterBool(settingsCategory, hiddenCustomSetting, false, "Enable Hidden Custom Nameplates");
            MelonPrefs.RegisterBool(settingsCategory, hideFriendsNameplates, false, "Hide Friends Nameplates");
            MelonPrefs.RegisterInt(settingsCategory, nameplateScaleSetting, 100, "Nameplate Size Percentage");
            MelonPrefs.RegisterBool(settingsCategory, dynamicResizerSetting, false, "Enable Dynamic Nameplate Resizer");
            MelonPrefs.RegisterFloat(settingsCategory, dynamicResizerDistance, 3f, "Dynamic Resizer Max Distance");

            //Register dynamic scaler
            ClassInjector.RegisterTypeInIl2Cpp<DynamicNameplateScaler>();

            //Initalize Harmony
            harmony = HarmonyInstance.Create("BTKStandalone");
            harmony.Patch(typeof(VRCAvatarManager).GetMethod("Method_Private_Boolean_GameObject_String_Single_PDM_0", BindingFlags.Instance | BindingFlags.Public), null, new HarmonyMethod(typeof(BTKSANameplateMod).GetMethod("OnAvatarInit", BindingFlags.NonPublic | BindingFlags.Static)));

            avatarDescriptProperty = typeof(VRCAvatarManager).GetProperty("prop_VRC_AvatarDescriptor_0", BindingFlags.Public | BindingFlags.Instance, null, typeof(VRC_AvatarDescriptor), new Type[0], null);

            //Load the settings to the local copy to compare with SettingsApplied
            getPrefsLocal();
        }

        public override void OnModSettingsApplied()
        {
            if(hiddenCustomLocal!=MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting) || hideFriendsLocal!= MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates) || scaleLocal!= MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting) || dynamicResizerLocal!= MelonPrefs.GetBool(settingsCategory, dynamicResizerSetting) || dynamicResDistLocal!= MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance))
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

                    //User is remote, apply fix
                    MelonLogger.Log($"New user or avatar change! Applying NameplateMod on { user.displayName }");
                    Vector3 npPos = nameplate.transform.position;
                    object avatarDescriptor = avatarDescriptProperty.GetValue(user.vrcPlayer.prop_VRCAvatarManager_0);
                    float viewPointY = 0;

                    //Get viewpoint for AV2 avatar
                    if (avatarDescriptor != null)
                        viewPointY = ((VRC_AvatarDescriptor)avatarDescriptor).ViewPosition.y;

                    //Get viewpoint for AV3 avatar
                    if (user.vrcPlayer.prop_VRCAvatarManager_0.prop_VRCAvatarDescriptor_0 != null)
                        viewPointY = user.vrcPlayer.prop_VRCAvatarManager_0.prop_VRCAvatarDescriptor_0.ViewPosition.y;

                    if (viewPointY>0)
                    {
                        npPos.y = viewPointY + user.vrcPlayer.transform.position.y + 0.5f;
                        nameplate.transform.position = npPos;
                    }

                    float nameplateScale = (MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting)/100f)*0.0015f;

                    //Disable nameplates on friends
                    if(MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates))
                    {
                        if (user.vrcPlayer.field_Private_APIUser_0.isFriend)
                        {
                            Vector3 newScale = new Vector3(0, 0, 0);
                            nameplate.transform.localScale = newScale;
                            friend = true;
                        }
                    }

                    //Check for DynamicNameplateScaler component
                    GameObject.Destroy(nameplate.GetComponent<DynamicNameplateScaler>());
                    if (nameplateScale != 0.0015f && !(friend && MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates)))
                    {
                        if (MelonPrefs.GetBool(settingsCategory, dynamicResizerSetting))
                        {
                            DynamicNameplateScaler component = nameplate.AddComponent<DynamicNameplateScaler>();
                            component.ApplySettings(user.vrcPlayer, nameplateScale, nameplateDefaultSize, MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance));
                        }
                        else
                        {
                            Vector3 newScale = new Vector3(nameplateScale, nameplateScale, nameplateScale);
                            nameplate.transform.localScale = newScale;
                        }
                    }

                    if (MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting))
                    {
                        Transform nameplateObject = user.avatarObject.transform.Find("Custom Nameplate");
                        if (nameplateObject != null && !nameplateObject.gameObject.active)
                        {
                            MelonLogger.Log($"Found hidden Custom Nameplate on { user.displayName }, enabling.");
                            nameplateObject.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        private void getPrefsLocal()
        {
            hiddenCustomLocal = MelonPrefs.GetBool(settingsCategory, hiddenCustomSetting);
            hideFriendsLocal = MelonPrefs.GetBool(settingsCategory, hideFriendsNameplates);
            scaleLocal = MelonPrefs.GetInt(settingsCategory, nameplateScaleSetting);
            dynamicResizerLocal = MelonPrefs.GetBool(settingsCategory, dynamicResizerSetting);
            dynamicResDistLocal = MelonPrefs.GetFloat(settingsCategory, dynamicResizerDistance);
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
