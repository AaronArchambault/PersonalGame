using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a quick scale+fade pop effect at a world position.
/// Called by Fruit.cs on merge.
/// </summary>
public class MergeEffect : MonoBehaviour
{
    public static void Spawn(Vector2 position, Color color)
    {
        GameObject go = new GameObject("MergeEffect");
        go.transform.position = position;
        go.AddComponent<MergeEffect>().Play(color);
    }

    void Play(Color color)
    {
        // Create a simple circle visual
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = FruitSpriteGenerator.CreateCircleSprite(color, Color.white * 0.8f);
        sr.sortingOrder = 10;
        StartCoroutine(AnimateEffect(sr));
    }

    IEnumerator AnimateEffect(SpriteRenderer sr)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.2f;
        Vector3 endScale = Vector3.one * 1.2f;

        while (elapsed < duration)
        { 
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            Color c = sr.color;
            c.a = 1f - t;
            sr.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}