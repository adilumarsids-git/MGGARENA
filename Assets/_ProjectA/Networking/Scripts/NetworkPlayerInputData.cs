using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public struct NetworkPlayerInputData : INetworkInput
    {
        public const int Jump = 0;
        public const int Basic = 1;
        public const int Active = 2;
        public const int Ultimate = 3;

        public Vector2 Move;
        public NetworkButtons Buttons;
    }
}
