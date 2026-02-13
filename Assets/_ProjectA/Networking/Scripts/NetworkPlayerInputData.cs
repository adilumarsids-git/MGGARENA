using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    public struct NetworkPlayerInputData : INetworkInput
    {
        public const int Jump = 0;
        public const int Action = 1;

        public Vector2 Move;
        public NetworkButtons Buttons;
    }
}
