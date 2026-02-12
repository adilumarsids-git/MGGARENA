#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectA.Editor
{
    public static class ProjectAPrefabTools
    {
        [MenuItem("ProjectA/Generate Network Player Prefabs")]
        public static void GenerateNetworkPrefabs()
        {
            var outputDir = "Assets/_ProjectA/Prefabs";
            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                AssetDatabase.CreateFolder("Assets/_ProjectA", "Prefabs");
            }

            for (int i = 1; i <= 5; i++)
            {
                var srcPath = $"Assets/_MyCharacters/Character{i}.prefab";
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
                if (!src) continue;

                var instance = PrefabUtility.InstantiatePrefab(src) as GameObject;
                if (!instance.GetComponent<Fusion.NetworkObject>()) instance.AddComponent<Fusion.NetworkObject>();
                if (!instance.GetComponent<NetworkPlayer_MOST>()) instance.AddComponent<NetworkPlayer_MOST>();
                if (!instance.GetComponent<BotBrain>()) instance.AddComponent<BotBrain>();

                var outPath = Path.Combine(outputDir, $"NetworkPlayer_Character{i}.prefab").Replace("\\", "/");
                PrefabUtility.SaveAsPrefabAsset(instance, outPath);
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectA network player prefabs generated.");
        }
    }
}
#endif
