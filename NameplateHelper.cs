using System;
using TMPro;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace BTKSANameplateMod
{
    public class NameplateHelper : MonoBehaviour
    {
        //Nameplate object references
        public Graphic uiIconBackground;
        public RawImage uiUserImage;
        public GameObject uiUserImageContainer;
        public ImageThreeSlice uiNameBackground;
        public ImageThreeSlice uiQuickStatsBackground;
        public TextMeshProUGUI uiName;

        private PlayerNameplate nameplate = null;
        private Color nameColour;
        private Color nameColour2;
        private bool setColour;
        private bool colourLerp;

        //Colour lerp stuff
        private bool lerpReverse = false;
        private float lerpValue = 0f;
        private float lerpTransitionTime = 3f;

        public NameplateHelper(IntPtr ptr) : base(ptr)
        {

        }

        [HideFromIl2Cpp]
        public void SetNameplate(PlayerNameplate nameplate)
        {
            this.nameplate = nameplate;
        }

        [HideFromIl2Cpp]
        public void SetNameColour(Color color)
        {
            this.nameColour = color;
            setColour = true;
        }

        [HideFromIl2Cpp]
        public void SetColourLerp(Color color1, Color color2)
        {
            this.nameColour = color1;
            this.nameColour2 = color2;

            setColour = false;
            colourLerp = true;
        }

        [HideFromIl2Cpp]
        public void ResetNameplate()
        {
            setColour = false;
            colourLerp = false;
        }

        [HideFromIl2Cpp]
        public void OnRebuild()
        {
            if (nameplate != null)
            {
                if (setColour)
                {
                    uiName.color = nameColour;
                }
            }
        }

        public void Update()
        {
            //Check if we should be doing the lerp
            if (colourLerp)
            {
                if (!lerpReverse)
                    lerpValue += Time.deltaTime;
                else
                    lerpValue -= Time.deltaTime;

                if (lerpValue >= lerpTransitionTime)
                {
                    lerpValue = lerpTransitionTime;
                    lerpReverse = true;
                }

                if (lerpValue <= 0)
                {
                    lerpValue = 0f;
                    lerpReverse = false;
                }

                uiName.color = Color.Lerp(nameColour, nameColour2, lerpValue);

            }
        }

    }
}
