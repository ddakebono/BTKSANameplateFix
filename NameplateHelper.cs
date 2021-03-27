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
        public GameObject uiQuickStatsGO;
        public ImageThreeSlice uiNameBackground;
        public ImageThreeSlice uiQuickStatsBackground;
        public TextMeshProUGUI uiName;
        public CanvasGroup uiGroup;
        public GameObject localPlayerGO;
        public float fadeMaxRange;
        public float fadeMinRange;
        public bool closeRangeFade = false;
        public bool AlwaysShowQuickInfo = false;

        private PlayerNameplate nameplate = null;
        private Color nameColour;
        private Color nameColour2;
        private bool setColour;
        private bool colourLerp;

        //Colour lerp stuff
        private bool lerpReverse = false;
        private float lerpValue = 0f;
        private float lerpTransitionTime = 3f;
        private bool closeRangeFadePause = false;

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
            AlwaysShowQuickInfo = false;
            closeRangeFade = false;
            if (uiGroup != null)
                uiGroup.alpha = 1;
        }

        [HideFromIl2Cpp]
        public void OnRebuild(bool QMOpen)
        {
            if (nameplate != null)
            {
                if (setColour)
                {
                    uiName.color = nameColour;
                }

                if (AlwaysShowQuickInfo)
                {
                    uiQuickStatsGO.SetActive(true);
                }

                if (closeRangeFade && uiGroup != null)
                {
                    //Reset and pause CloseRangeFade
                    if (QMOpen)
                    {
                        closeRangeFadePause = true;
                        uiGroup.alpha = 1;
                    }
                    else
                    {
                        closeRangeFadePause = false;
                    }
                }
            }
        }

        public void Update()
        {
            if (closeRangeFade && uiGroup != null && localPlayerGO != null && !closeRangeFadePause)
            {
                float distance = Vector3.Distance(nameplate.gameObject.transform.position, localPlayerGO.transform.position);
                if (distance < fadeMaxRange && distance > fadeMinRange)
                {
                    //Find our alpha value
                    float alphaValue = (distance - fadeMinRange) / (fadeMaxRange - fadeMinRange);

                    uiGroup.alpha = alphaValue;
                }

                if (distance >= fadeMaxRange)
                    uiGroup.alpha = 1;

                if (distance <= fadeMinRange)
                    uiGroup.alpha = 0;
            }

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
