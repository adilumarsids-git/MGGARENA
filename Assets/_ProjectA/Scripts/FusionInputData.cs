using Fusion;
using UnityEngine;

namespace ProjectA
{
    public struct FusionInputData : INetworkInput
    {
        public Vector2 Move;
        public Vector2 Aim;
        public NetworkBool ShootHeld;
        public NetworkBool ThrowPressed;
        public NetworkBool ActivePressed;
        public NetworkBool UltimatePressed;
    }
}
