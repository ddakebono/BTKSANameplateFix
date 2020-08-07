﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRC;

namespace BTKSANameplateFix
{
    public class GDBUser
    {
        public Player vrcPlayer;
        public string displayName;
        public GameObject avatarObject;

        public GDBUser(Player vrcPlayer, string displayName)
        {
            this.vrcPlayer = vrcPlayer;
            this.displayName = displayName;
            this.avatarObject = vrcPlayer.field_Internal_VRCPlayer_0.field_Internal_GameObject_0;
        }
    }
}