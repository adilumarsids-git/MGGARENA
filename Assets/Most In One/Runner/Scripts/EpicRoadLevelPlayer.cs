using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class EpicRoadLevelPlayer : MonoBehaviour
    {
        public void PlayAll()
        {
            MOST_Spawn[] sps = FindObjectsByType<MOST_Spawn>(FindObjectsSortMode.None);
            foreach (MOST_Spawn sp in sps) sp.EnableState(true);

            ForwardMovement[] fms = FindObjectsByType<ForwardMovement>(FindObjectsSortMode.None);
            foreach (ForwardMovement sp in fms) sp.Enabled = true;

            WalkEnemyManager[] ens = FindObjectsByType<WalkEnemyManager>(FindObjectsSortMode.None);
            foreach (WalkEnemyManager sp in ens) sp.StartMove = true;
        }
    }
}
