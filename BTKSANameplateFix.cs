using Harmony;
using MelonLoader;
using Org.BouncyCastle.Math.Raw;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRC;

namespace BTKSANameplateFix
{
    public static class BuildInfo
    {
        public const string Name = "BTKSANameplateFix"; // Name of the Mod.  (MUST BE SET)
        public const string Author = "DDAkebono#0001"; // Author of the Mod.  (Set as null if none)
        public const string Company = "BTK-Development"; // Company that made the Mod.  (Set as null if none)
        public const string Version = "1.0.0"; // Version of the Mod.  (MUST BE SET)
        public const string DownloadLink = "https://github.com/ddakebono/BTKSANameplateFix/releases"; // Download Link for the Mod.  (Set as null if none)
    }

    public class BTKSANameplateFix : MelonMod
    {
        public static BTKSANameplateFix instance;

        public HarmonyInstance harmony;

        private string settingsCategory = "BTKSANameplateFix";
        private string hiddenCustomSetting = "enableHiddenCustomNameplates";

        public override void VRChat_OnUiManagerInit()
        {
            MelonModLogger.Log("BTK Standalone: Nameplate Fix - Starting up");

            instance = this;

            if (Directory.Exists("BTKCompanion"))
            {
                MelonModLogger.Log("Woah, hold on a sec, it seems you might be running BTKCompanion, if this is true NameplateFix is built into that, and you should not be using this!");
                MelonModLogger.Log("If you are not currently using BTKCompanion please remove the BTKCompanion folder from your VRChat installation!");
                MelonModLogger.LogError("Nameplate Fix has not started up! (BTKCompanion Exists)");
                return;
            }

            ModPrefs.RegisterCategory(settingsCategory, "Nameplate Fix");
            ModPrefs.RegisterPrefBool(settingsCategory, hiddenCustomSetting, false, "Enable Hidden Custom Nameplates");

            //Initalize Harmony
            harmony = HarmonyInstance.Create("BTKStandalone");
            harmony.Patch(typeof(VRCAvatarManager).GetMethod("Method_Private_Boolean_GameObject_String_Single_0", BindingFlags.Instance | BindingFlags.Public), null, new HarmonyMethod(typeof(BTKSANameplateFix).GetMethod("OnAvatarInit", BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void OnAvatarInit(GameObject __0, ref VRCAvatarManager __instance, ref bool __result)
        {
            if (__instance.field_Private_VRCPlayer_0.field_Private_Player_0 != null)
            {
                if (__instance.field_Private_VRCPlayer_0.field_Private_Player_0.field_Private_APIUser_0 != null)
                {
                    //User changed avatar, send to GDBManager for processing
                    BTKSANameplateFix.instance.OnUpdatePlayer(__instance.field_Private_VRCPlayer_0.field_Private_Player_0);
                }
            }

        }

        public void OnUpdatePlayer(Player player)
        {
            if (ValidatePlayerAvatar(player))
            {
                GDBUser user = new GDBUser(player, player.field_Private_APIUser_0.id);
                if (!player.name.Contains("Local"))
                {
                    //User is remote, apply fix
                    MelonModLogger.Log($"New user or avatar change! Applying Fix on { user.displayName }");
                    Vector3 npPos = user.vrcPlayer.field_Internal_VRCPlayer_0.field_Private_VRCWorldPlayerUiProfile_0.gameObject.transform.position;
                    if (user.vrcPlayer.prop_VRCAvatarManager_0.prop_VRC_AvatarDescriptor_0 != null)
                    {
                        npPos.y = user.vrcPlayer.field_Private_VRCAvatarManager_0.prop_VRC_AvatarDescriptor_0.ViewPosition.y + user.vrcPlayer.transform.position.y + 0.5f;
                        user.vrcPlayer.field_Internal_VRCPlayer_0.field_Private_VRCWorldPlayerUiProfile_0.gameObject.transform.position = npPos;
                    }

                    if (ModPrefs.GetBool(settingsCategory, hiddenCustomSetting))
                    {
                        Transform nameplateObject = user.avatarObject.transform.Find("Custom Nameplate");
                        if (nameplateObject != null && !nameplateObject.gameObject.active)
                        {
                            MelonModLogger.Log($"Found hidden Custom Nameplate on { user.displayName }, enabling.");
                            nameplateObject.gameObject.SetActive(true);
                        }
                    }
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
