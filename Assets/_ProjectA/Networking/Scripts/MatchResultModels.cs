using System.Collections.Generic;

namespace ProjectA.Networking
{
    public sealed class MatchResultEntry
    {
        public string nickname;
        public int rank;
        public int score;
    }

    public static class MatchResultsData
    {
        private static readonly List<MatchResultEntry> EntriesInternal = new();

        public static FusionGameMode Mode { get; private set; } = FusionGameMode.FFA;
        public static IReadOnlyList<MatchResultEntry> Entries => EntriesInternal;

        public static void Clear(FusionGameMode mode)
        {
            Mode = mode;
            EntriesInternal.Clear();
        }

        public static void Add(string nickname, int rank, int score)
        {
            EntriesInternal.Add(new MatchResultEntry
            {
                nickname = nickname,
                rank = rank,
                score = score
            });
        }
    }
}
