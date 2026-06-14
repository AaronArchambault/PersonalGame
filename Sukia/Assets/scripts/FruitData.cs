using UnityEngine;

[CreateAssetMenu(fileName = "FruitData", menuName = "SuikaGame/FruitData")]
public class FruitData : ScriptableObject
{
    [System.Serializable]
    public class FruitInfo
    {
        public string fruitName;
        public float radius;
        public int points;
        public Color color;
        public Sprite sprite; // optional - assign in Inspector
    }

    public FruitInfo[] fruits; // index 0 = smallest, ascending in size
} 