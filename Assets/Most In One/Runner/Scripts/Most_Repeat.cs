using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Solo.MOST_IN_ONE
{
    public class Most_Repeat : MonoBehaviour // Marked as legacy
    {
        [BigHeader("Prefabs")]
        [Tooltip("the Spawned (Repeat) object Prefab (Step)")]
        public GameObject RepeatedPrefab;

        [Tooltip("(Optional) the Parent in scene to all spawn objects")]
        public GameObject AsChildTo;

        [Tooltip("Number of spawned Steps")]
        [Min(1)] public int Amount;

        [Tooltip("additional amount for each Step (Starting from 1.0)")]
        [Min(.01f)] public float ScoreMultiplayerJump;

        [BigHeader("Position Settings")]
        [Tooltip("Offset betwen each Step")]
        public Vector3 Offset;

        [Tooltip("Start position of the first Step")]
        public Vector3 StartWorldPosition;

        [BigHeader("Target Score Text")]
        [Tooltip("these texts will be set starting from 1 increased by ScoreMultiplayerJump")]
        public string[] TextNameInStepChilds;

        [BigHeader("Colored Object loop Set")]
        [Tooltip("loop and change each target's color")]
        public string TargetColorMaterialLerpName;

        [Tooltip("looped material  list")]
        public List<Material> MaterialsLerp;

        void Start()
        {
            Vector3 tmpPoint = StartWorldPosition; float jump = 1;
            for (int count = 0; count < Amount; count++) // Spawn obejcts loop
            {
                GameObject newObj = Instantiate(RepeatedPrefab, tmpPoint, Quaternion.identity, AsChildTo ? AsChildTo.transform : null);

                // Loop for all recorded Texts inside the object and update it's text
                foreach (string mText in TextNameInStepChilds) newObj.transform.Find(mText).GetComponent<TMP_Text>().text = "x" + jump.ToString("0.0");

                // Loop for all rendered objects and change the colors
                newObj.transform.Find(TargetColorMaterialLerpName).GetComponent<Renderer>().material = MaterialsLerp[count % MaterialsLerp.Count];

                // update the offset for the next spawn object
                tmpPoint += Offset; jump += ScoreMultiplayerJump;
            }
        }
    }
}
