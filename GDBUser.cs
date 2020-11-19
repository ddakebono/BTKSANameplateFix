using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRC;

namespace BTKSANameplateMod
{
    public class GDBUser
    {
        public Player vrcPlayer;
        public GameObject avatarObject;

        public GDBUser(Player vrcPlayer)
        {
            this.vrcPlayer = vrcPlayer;
            this.avatarObject = vrcPlayer.field_Internal_VRCPlayer_0.field_Internal_GameObject_0;
        }
    }
}
