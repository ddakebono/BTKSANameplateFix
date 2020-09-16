using System;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using VRC;

namespace BTKSANameplateMod
{
    public class DynamicNameplateScaler : MonoBehaviour
    {
        private Player user;
        private float minSize;
        private float maxSize;
        private float maxDist;
        private float scaleDiff;

        public DynamicNameplateScaler(IntPtr ptr) : base(ptr)
        {

        }

        [HideFromIl2Cpp]
        public void ApplySettings(Player user, float minSize, float maxSize, float maxDist)
        {
            this.user = user;
            this.minSize = minSize;
            this.maxSize = maxSize;
            this.maxDist = maxDist;

            this.scaleDiff = maxSize - minSize;

            setNameplateScale(true);
        }

        public void Update()
        {
            setNameplateScale();
        }

        [HideFromIl2Cpp]
        public void setNameplateScale(bool forceScale = false)
        {
            if (ValidatePlayer(Player.prop_Player_0))
            {
                float currentDist = Vector3.Distance(Player.prop_Player_0.field_Internal_VRCPlayer_0.transform.position, user.transform.position);
                if (currentDist <= maxDist || forceScale)
                {
                    float nameplateScale = (scaleDiff * (currentDist / maxDist))+minSize;
                    Vector3 newScale = new Vector3(nameplateScale, nameplateScale, nameplateScale);
                    user.field_Internal_VRCPlayer_0.field_Private_VRCWorldPlayerUiProfile_0.gameObject.transform.localScale = newScale;
                }
            }
        }

        [HideFromIl2Cpp]
        bool ValidatePlayer(Player player)
        {
            return !(player == null ||
                     player.field_Internal_VRCPlayer_0 == null ||
                     player.field_Internal_VRCPlayer_0.isActiveAndEnabled == false);
        }
    }
}
