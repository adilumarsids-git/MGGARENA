using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectA.Core
{
    public enum ProjectAEnvironment
    {
        Dev,
        Stage,
        Prod
    }

    [Serializable]
    public class RoomDefinition
    {
        public string roomId;
        public int entryFee;
        public int maxPlayers;
        public string mode;
    }

    [CreateAssetMenu(fileName = "ProjectAConfig", menuName = "ProjectA/Configuration", order = 1)]
    public class ProjectAConfig : ScriptableObject
    {
        [Header("Backend")]
        [SerializeField] private string backendBaseUrl = "http://localhost:8080";
        [SerializeField] private ProjectAEnvironment environment = ProjectAEnvironment.Dev;

        [Header("Fusion (Placeholder)")]
        [SerializeField] private string fusionAppId = "REPLACE_WITH_FUSION_APP_ID";
        [SerializeField] private string fusionRegion = "REPLACE_WITH_REGION";

        [Header("Rooms")]
        [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>
        {
            new RoomDefinition { roomId = "bronze-001", entryFee = 100, maxPlayers = 4, mode = "duel" },
            new RoomDefinition { roomId = "silver-001", entryFee = 500, maxPlayers = 8, mode = "battle" }
        };

        public string BackendBaseUrl => backendBaseUrl;
        public ProjectAEnvironment Environment => environment;
        public string FusionAppId => fusionAppId;
        public string FusionRegion => fusionRegion;
        public IReadOnlyList<RoomDefinition> Rooms => rooms;
    }
}
