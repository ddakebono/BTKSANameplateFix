using System.Text;
using Il2CppSystem.Security.Cryptography;
using MelonLoader;
using UnityEngine;
using VRC;
using VRC.Core;
using VRC.DataModel.Core;
using VRC.UI.Elements.Menus;

namespace BTKSANameplateMod
{
    public static class BTKUtil
    {
        private static MD5 _hasher = MD5.Create();
        
        private static SelectedUserMenuQM _selectedUserMenuQM;
        
        public static int Combine(this byte b1, byte concat)
        {
            int combined = b1 << 8 | concat;
            return combined;
        }
        
        public static void Log(string log, bool dbg = false)
        {
            if (!MelonDebug.IsEnabled() && dbg)
                return;

            MelonLogger.Msg(log);
        }

        public static Color GetColourFromUserID(string userID)
        {
            var hash = _hasher.ComputeHash(Encoding.UTF8.GetBytes(userID));
            int colour2 = hash[3].Combine(hash[4]);
            //Fixed saturation and brightness values, only hue is altered
            return Color.HSVToRGB(colour2 / 65535f, .8f, .8f);
        }
        
        public static APIUser GetSelectedAPIUser()
        {
            if (_selectedUserMenuQM == null)
                _selectedUserMenuQM = Object.FindObjectOfType<SelectedUserMenuQM>();

            if (_selectedUserMenuQM != null)
            {
                DataModel<APIUser> user = _selectedUserMenuQM.field_Private_IUser_0.Cast<DataModel<APIUser>>();
                return user.field_Protected_TYPE_0;
            }

            MelonLogger.Error("Unable to get SelectedUserMenuQM component!");
            return null;
        }
        
        public static Player getPlayerFromPlayerlist(string userID)
        {
            foreach (var player in PlayerManager.field_Private_Static_PlayerManager_0.field_Private_List_1_Player_0)
            {
                if (player.prop_APIUser_0 != null)
                {
                    if (player.prop_APIUser_0.id.Equals(userID))
                        return player;
                }
            }

            return null;
        }
        
        public static bool ValidatePlayerAvatar(VRCPlayer player)
        {
            return !(player == null ||
                     player.isActiveAndEnabled == false ||
                     player.field_Internal_Animator_0 == null ||
                     player.field_Internal_GameObject_0 == null ||
                     player.field_Internal_GameObject_0.name.IndexOf("Avatar_Utility_Base_") == 0);
        }
    }
}