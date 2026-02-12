#if UNITY_EDITOR
using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [CreateAssetMenu(fileName = "RunnerLevelProfile", menuName = "MOST/Runner Level Profile")]
    public class RunnerLevelProfile : ScriptableObject
    {
        public RunnerLevelGeneratorWindow.LayoutConfig layout;
        public RunnerLevelGeneratorWindow.EndgameConfig endgame;
        public RunnerLevelGeneratorWindow.BonusRepeatConfig bonus;
        public RunnerLevelGeneratorWindow.ObjectsConfig objectsCfg;
        public RunnerLevelGeneratorWindow.OutputConfig output;
    }
}
#endif
