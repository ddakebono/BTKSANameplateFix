﻿using MelonLoader;
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
                    nameplate.uiName.color = nameColour;
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

                if(lerpValue >= lerpTransitionTime)
                {
                    lerpValue = lerpTransitionTime;
                    lerpReverse = true;
                }

                if (lerpValue <= 0)
                {
                    lerpValue = 0f;
                    lerpReverse = false;
                }

                nameplate.uiName.color = Color.Lerp(nameColour, nameColour2, lerpValue);

            }
        }

    }
}
