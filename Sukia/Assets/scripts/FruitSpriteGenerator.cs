using UnityEngine;

/// <summary>
/// Utility to generate a circle sprite at runtime.
/// Attach to any GameObject or use statically.
/// </summary>
public static class FruitSpriteGenerator
{
    private static readonly int TEX_SIZE = 128;

    public static Sprite CreateCircleSprite(Color color, Color borderColor, float borderFraction = 0.08f)
    {
        Texture2D tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float center = TEX_SIZE / 2f;
        float outerRadius = TEX_SIZE / 2f - 1f;
        float innerRadius = outerRadius * (1f - borderFraction);

        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));

                if (dist > outerRadius)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
                else if (dist > innerRadius)
                { 
                    // Anti-alias the border
                    float t = Mathf.InverseLerp(outerRadius, innerRadius, dist);
                    Color c = Color.Lerp(Color.clear, borderColor, t);
                    tex.SetPixel(x, y, c);
                }
                else
                {
                    // Slight gradient shading
                    float shade = 1f - (dist / outerRadius) * 0.2f;
                    tex.SetPixel(x, y, new Color(color.r * shade, color.g * shade, color.b * shade, 1f));
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TEX_SIZE, TEX_SIZE), new Vector2(0.5f, 0.5f), TEX_SIZE);
    }
}