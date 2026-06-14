#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity Editor utility to auto-populate a FruitData asset with 11 default fruits.
/// Go to Tools > Suika Game > Setup Default Fruit Data
/// </summary>
public class FruitDataSetup : EditorWindow
{
    [MenuItem("Tools/Suika Game/Setup Default Fruit Data")]
    static void SetupDefaultFruitData()
    {
        // Find or create FruitData asset
        string path = "Assets/SuikaGame/FruitData.asset";
        System.IO.Directory.CreateDirectory("Assets/SuikaGame");

        FruitData data = AssetDatabase.LoadAssetAtPath<FruitData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<FruitData>();
            AssetDatabase.CreateAsset(data, path);
        }

        // 11 fruits matching Suika game progression (Cherry → Watermelon)
        data.fruits = new FruitData.FruitInfo[]
        {
            new FruitData.FruitInfo { fruitName = "Cherry",      radius = 0.22f, points = 1,   color = new Color(0.86f, 0.08f, 0.24f) },
            new FruitData.FruitInfo { fruitName = "Strawberry",  radius = 0.30f, points = 3,   color = new Color(0.99f, 0.20f, 0.23f) },
            new FruitData.FruitInfo { fruitName = "Grape",       radius = 0.38f, points = 6,   color = new Color(0.56f, 0.22f, 0.74f) },
            new FruitData.FruitInfo { fruitName = "Dekopon",     radius = 0.46f, points = 10,  color = new Color(1.00f, 0.60f, 0.10f) },
            new FruitData.FruitInfo { fruitName = "Orange",      radius = 0.54f, points = 15,  color = new Color(1.00f, 0.50f, 0.00f) },
            new FruitData.FruitInfo { fruitName = "Apple",       radius = 0.62f, points = 21,  color = new Color(0.90f, 0.12f, 0.12f) },
            new FruitData.FruitInfo { fruitName = "Pear",        radius = 0.72f, points = 28,  color = new Color(0.84f, 0.90f, 0.20f) },
            new FruitData.FruitInfo { fruitName = "Peach",       radius = 0.84f, points = 36,  color = new Color(1.00f, 0.72f, 0.60f) },
            new FruitData.FruitInfo { fruitName = "Pineapple",   radius = 0.96f, points = 45,  color = new Color(0.96f, 0.88f, 0.10f) },
            new FruitData.FruitInfo { fruitName = "Melon",       radius = 1.10f, points = 55,  color = new Color(0.40f, 0.86f, 0.40f) },
            new FruitData.FruitInfo { fruitName = "Watermelon",  radius = 1.28f, points = 100, color = new Color(0.15f, 0.68f, 0.15f) },
        };

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("✅ FruitData created at " + path + " with 11 fruits!");
        EditorUtility.DisplayDialog("Suika Game Setup",
            "FruitData asset created at:\n" + path +
            "\n\nNow:\n1. Create an empty scene\n2. Add an empty GameObject\n3. Add SceneBootstrapper to it\n4. Assign the FruitData asset\n5. Press Play!",
            "Got it!");
    }

   // [MenuItem("Tools/Suika Game/Create Scene Bootstrapper")]
   /* static void CreateBootstrapper()
    {
       // GameObject go = new GameObject("Bootstrapper");
       // go.AddComponent<SceneBootstrapper>();

        // Try to auto-assign FruitData
        FruitData data = AssetDatabase.LoadAssetAtPath<FruitData>("Assets/SuikaGame/FruitData.asset");
        if (data != null)
        {
            go.GetComponent<SceneBootstrapper>().fruitData = data;
            Debug.Log("✅ FruitData auto-assigned to Bootstrapper!");
        }
        else
        {
            Debug.LogWarning("⚠️ FruitData not found. Please run 'Tools > Suika Game > Setup Default Fruit Data' first.");
        }

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }*/
}
#endif 