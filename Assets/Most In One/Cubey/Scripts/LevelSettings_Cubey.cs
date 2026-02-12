using UnityEngine;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField, DisallowMultipleComponent]
    public class LevelSettings_Cubey : MonoBehaviour
    {
        // Current Level Progress for checking // you can calll these attributes to check the current progress
        [ReadOnly, Tooltip("ReadOnly: The current remain blocks")]
        public int BlockRemains = 1;

        [ReadOnly, Tooltip("ReadOnly: The current percentage completed")]
        public float PercentageCompleted = 0;

        // ReadOnly: separte progress bar to steps, this value equal to one step on x axis
        [HideInInspector] public float ProgressBarSteps { get; private set; } 

        [Line, Tooltip("The middle Point is Zero so when scale is 16 the level range will start from -8 to 8")]
        public Vector2 LevelScale; // The middle Point is Zero so when scale is 16 the level range will start from -8 to 8...

        [Required("(As Prefab)"), Tooltip("Spawnd Block prefab")]
        public GameObject BlockPrefab;

        [Required("(As Child)"), Tooltip("Start Block object in scene")]
        public GameObject StartBlock;

        [Required("(As Child)"), Tooltip("End Block object in scene")]
        public GameObject EndBlock;

        [Line, Tooltip("This Layer Mask represent which layers will be ignored when build the level(The points will be not filled with Blocks Like Walls)")]
        public LayerMask IgnoredPoints;

        [Min(.1f), Tooltip("The transform scale of the block so it will be used to sort the the level blocks")]
        public float BlockScale = 2;

        [Min(.1f), Tooltip("the distance between the start progeress position and end progress position\nused to calculate the progress per block")]
        public float ProgressParScale = 300;

        void Awake()
        {
            for (float i = -LevelScale.x / 2; i <= LevelScale.x / 2; i += BlockScale)  // this loop split the level to squares and move across it horizontally and...
                for (float j = -LevelScale.y / 2; j <= LevelScale.y / 2; j += BlockScale) // vertically
                {
                    Vector3 currentSquarePos = new(i, 0, j);
                    Ray ray = new(currentSquarePos + Vector3.up * 3, transform.TransformDirection(Vector3.down));
                    Physics.Raycast(ray, out RaycastHit hit, 5, layerMask: IgnoredPoints);
                    // if (this square is empty and the this square position not the start nor end positions
                    if (hit.collider == null && currentSquarePos != StartBlock.transform.position && currentSquarePos != EndBlock.transform.position)
                    {
                        Instantiate(BlockPrefab, currentSquarePos, Quaternion.identity, transform);
                        BlockRemains++;
                    }
                }
            ProgressBarSteps = ProgressParScale / BlockRemains;
        }
    }
}
