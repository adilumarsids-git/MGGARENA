using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace ProjectA.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [SerializeField] private float matchDurationSeconds = 180f;
        [SerializeField] private FusionGameMode configuredMode = FusionGameMode.FFA;

        [Networked] public TickTimer MatchTimer { get; private set; }
        [Networked] public NetworkBool MatchLocked { get; private set; }
        [Networked] public int TeamAScore { get; private set; }
        [Networked] public int TeamBScore { get; private set; }
        [Networked] public int ModeValue { get; private set; }

        private readonly Dictionary<int, int> _scoreByPlayer = new();
        private readonly Dictionary<int, string> _nicknameByPlayer = new();

        public bool IsMatchLocked => MatchLocked;
        public FusionGameMode Mode => (FusionGameMode)ModeValue;

        public override void Spawned()
        {
            Instance = this;

            if (Object != null && Object.HasStateAuthority)
            {
                StartMatch(configuredMode, matchDurationSeconds);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || MatchLocked)
            {
                return;
            }

            if (MatchTimer.IsRunning && MatchTimer.Expired(Runner))
            {
                EndMatch();
            }
        }

        public void Configure(FusionGameMode mode, float durationSeconds)
        {
            configuredMode = mode;
            matchDurationSeconds = durationSeconds;

            if (Runner == null || Object == null || !Object.HasStateAuthority)
            {
                return;
            }

            StartMatch(configuredMode, matchDurationSeconds);
        }

        public void RegisterNickname(int playerRaw, string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                return;
            }

            _nicknameByPlayer[playerRaw] = nickname;
        }

        public void RegisterElimination(int attackerRaw, int victimRaw)
        {
            if (!Object.HasStateAuthority || MatchLocked)
            {
                return;
            }

            if (attackerRaw == victimRaw)
            {
                return;
            }

            _scoreByPlayer.TryGetValue(attackerRaw, out var current);
            _scoreByPlayer[attackerRaw] = current + 1;

            if (Mode == FusionGameMode.Teams)
            {
                if (ResolveTeam(attackerRaw) == 0) TeamAScore++;
                else TeamBScore++;
            }

            RPC_UpdateScore(attackerRaw, _scoreByPlayer[attackerRaw], TeamAScore, TeamBScore);
        }

        private void StartMatch(FusionGameMode mode, float durationSeconds)
        {
            ModeValue = (int)mode;
            MatchLocked = false;
            TeamAScore = 0;
            TeamBScore = 0;
            MatchTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
            _scoreByPlayer.Clear();

            MatchResultsData.Clear(mode);
        }

        private void EndMatch()
        {
            MatchLocked = true;

            var ranking = BuildRanking();
            RPC_ClearResults(ModeValue);

            var rank = 1;
            foreach (var raw in ranking)
            {
                _scoreByPlayer.TryGetValue(raw, out var score);
                RPC_AddResult(raw, rank, score);
                rank++;
            }
        }

        private List<int> BuildRanking()
        {
            var allPlayers = new List<int>();
            foreach (var player in Runner.ActivePlayers)
            {
                var raw = player.RawEncoded;
                if (!_scoreByPlayer.ContainsKey(raw))
                {
                    _scoreByPlayer[raw] = 0;
                }

                allPlayers.Add(raw);
            }

            if (Mode == FusionGameMode.Teams)
            {
                allPlayers.Sort((a, b) =>
                {
                    var teamA = ResolveTeam(a);
                    var teamB = ResolveTeam(b);
                    var scoreTeamA = teamA == 0 ? TeamAScore : TeamBScore;
                    var scoreTeamB = teamB == 0 ? TeamAScore : TeamBScore;
                    var compareTeam = scoreTeamB.CompareTo(scoreTeamA);
                    if (compareTeam != 0) return compareTeam;

                    var compareIndividual = _scoreByPlayer[b].CompareTo(_scoreByPlayer[a]);
                    if (compareIndividual != 0) return compareIndividual;

                    return a.CompareTo(b);
                });

                return allPlayers;
            }

            allPlayers.Sort((a, b) =>
            {
                var compareScore = _scoreByPlayer[b].CompareTo(_scoreByPlayer[a]);
                if (compareScore != 0) return compareScore;
                return a.CompareTo(b);
            });

            return allPlayers;
        }

        private int ResolveTeam(int playerRaw)
        {
            return Mathf.Abs(playerRaw) % 2;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ClearResults(int modeValue)
        {
            MatchResultsData.Clear((FusionGameMode)modeValue);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_AddResult(int playerRaw, int rank, int score)
        {
            var nickname = _nicknameByPlayer.TryGetValue(playerRaw, out var name)
                ? name
                : $"Player {playerRaw}";

            MatchResultsData.Add(nickname, rank, score);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateScore(int playerRaw, int score, int teamAScore, int teamBScore)
        {
            _scoreByPlayer[playerRaw] = score;
            TeamAScore = teamAScore;
            TeamBScore = teamBScore;
        }
    }
}
