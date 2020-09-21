using System;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using VRC;

namespace BTKSANameplateMod
{
    public class DynamicScalerCustom : MonoBehaviour
    {
        private Player user;
        private Material borderTarget;
        private Material tagTarget;
        private float minSize;
        private float maxSize;
        private float maxDist;
        private float scaleDiff;

        public DynamicScalerCustom(IntPtr ptr) : base(ptr)
        {

        }

        [HideFromIl2Cpp]
        public void ApplySettings(Player user, Material borderTarget, Material tagTarget, float minSize, float maxSize, float maxDist)
        {
            this.user = user;
            this.borderTarget = borderTarget;
            this.tagTarget = tagTarget;
            this.minSize = minSize;
            this.maxSize = maxSize;
            this.maxDist = maxDist;

            this.scaleDiff = maxSize - minSize;
        }

        public void Update()
        {
            setNameplateScale();
        }

        [HideFromIl2Cpp]
        public void setNameplateScale()
        {
            if (ValidatePlayer(Player.prop_Player_0))
            {
                float currentDist = Vector3.Distance(Player.prop_Player_0.field_Internal_VRCPlayer_0.transform.position, user.transform.position) - .2f;
                if (currentDist <= maxDist && currentDist >= 0)
                {
                    float nameplateScale = (scaleDiff * (currentDist / maxDist)) + minSize;
                    borderTarget.SetFloat("_Scale", nameplateScale);
                    tagTarget.SetFloat("_Scale", nameplateScale);
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
