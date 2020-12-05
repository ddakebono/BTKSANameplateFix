using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerBaseLib.Attributes;
using UnityEngine;

namespace BTKSANameplateMod
{
    public class NameplateHelper : MonoBehaviour
    {

        private PlayerNameplate nameplate = null;
        private Texture customIcon = null;
        private Color nameColour = Color.white;

        public NameplateHelper(IntPtr ptr) : base(ptr)
        {

        }

        [HideFromIl2Cpp]
        public void SetNameplate(PlayerNameplate nameplate)
        {
            this.nameplate = nameplate;
        }

        [HideFromIl2Cpp]
        public void SetCustomIcon(Texture customIcon)
        {
            this.customIcon = customIcon;
        }

        [HideFromIl2Cpp]
        public void SetNameColour(Color color)
        {
            this.nameColour = color;
        }

        [HideFromIl2Cpp]
        public void OnRebuild()
        {
            MelonLogger.Log("NameplateHelper Component Got OnRebuild!");

            if (nameplate != null)
            {
                if (customIcon != null)
                {
                    nameplate.uiIconBackground.enabled = true;
                    nameplate.uiUserImage.enabled = true;
                    nameplate.uiUserImageContainer.SetActive(true);

                    nameplate.uiUserImage.texture = customIcon;
                }

                if (nameColour != Color.white)
                {
                    nameplate.uiName.color = nameColour;
                }
            }
        }


    }
}
