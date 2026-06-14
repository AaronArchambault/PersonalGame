using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Fruit : MonoBehaviour
{
    public int fruitIndex { get; private set; }
    public bool hasLanded { get; private set; } = false;
    public bool isMerging { get; private set; } = false;

    private Rigidbody2D rb;
    private CircleCollider2D col;
    private SpriteRenderer spriteRenderer;
    private FruitData fruitData;

    private float landCheckDelay = 0.15f;
    private float velocityThreshold = 0.05f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(int index, FruitData data)
    {
        fruitIndex = index;
        fruitData = data;

        // Awake may not have run if the GameObject was inactive when instantiated —
        // fetch components here as a guaranteed fallback.
        if (rb == null)             rb = GetComponent<Rigidbody2D>();
        if (col == null)            col = GetComponent<CircleCollider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        var info = data.fruits[index];

        // Set collider size
        col.radius = info.radius;

        // Set visual
        transform.localScale = Vector3.one * info.radius * 2f;

        if (info.sprite != null)
            spriteRenderer.sprite = info.sprite;
        else
            spriteRenderer.color = info.color;

        // Also generate a circle sprite so the fruit is always visible
        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = FruitSpriteGenerator.CreateCircleSprite(info.color, Color.white * 0.6f);

        // Physics settings
        rb.gravityScale = 1.5f;
        rb.linearDamping = 0.3f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        gameObject.name = info.fruitName;
    }

    public void Drop()
    {
        if (col == null) col = GetComponent<CircleCollider2D>();

        // Move back to Default layer so collisions are active
        gameObject.layer = LayerMask.NameToLayer("Default");

        // Re-enable collider and switch to dynamic one frame later so
        // PhysX doesn't see an overlap and eject the fruit violently
        StartCoroutine(EnableAndDrop());
    }

    IEnumerator EnableAndDrop()
    {
        // Wait one fixed update so any held-fruit overlap is cleared
        yield return new WaitForFixedUpdate();
        col.enabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        StartCoroutine(LandCheck());
    }

    IEnumerator LandCheck()
    {
        yield return new WaitForSeconds(landCheckDelay);
        while (!hasLanded)
        {
            yield return new WaitForSeconds(0.1f);
            if (rb.linearVelocity.magnitude < velocityThreshold)
                hasLanded = true;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isMerging) return;

        Fruit other = collision.gameObject.GetComponent<Fruit>();
        if (other == null || other.isMerging) return;
        if (other.fruitIndex != fruitIndex) return;

        // Prevent merging the largest fruit (last in the cycle)
        if (fruitIndex >= fruitData.fruits.Length - 1) return;

        // Only the fruit with the lower instance ID initiates to avoid double merge
        if (gameObject.GetInstanceID() < other.gameObject.GetInstanceID())
        {
            isMerging = true;
            other.isMerging = true;
            StartCoroutine(MergeWith(other));
        }
    }

    IEnumerator MergeWith(Fruit other)
    {
        yield return new WaitForSeconds(0.05f);

        Vector2 midPoint = (transform.position + other.transform.position) / 2f;

        // Spawn merged fruit
        GameManager.Instance.SpawnMergedFruit(fruitIndex + 1, midPoint);

        // Award points
        int earnedPoints = fruitData.fruits[fruitIndex + 1].points;
        GameManager.Instance.AddScore(earnedPoints);

        // Pop effect
        MergeEffect.Spawn(midPoint, fruitData.fruits[fruitIndex + 1].color);

        Destroy(other.gameObject);
        Destroy(gameObject);
    }

    public void MakeKinematic()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (col == null) col = GetComponent<CircleCollider2D>();

        // Put on Held layer (no collision with anything) while player is aiming
        gameObject.layer = LayerMask.NameToLayer("Held");

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        col.enabled = false;
    }
}












/*using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Fruit : MonoBehaviour
{
    public int fruitIndex { get; private set; }
    public bool hasLanded { get; private set; } = false;
    public bool isMerging { get; private set; } = false;

    private Rigidbody2D rb;
    private CircleCollider2D col;
    private SpriteRenderer spriteRenderer;
    private FruitData fruitData;

    private float landCheckDelay = 0.15f;
    private float velocityThreshold = 0.05f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(int index, FruitData data)
    {
        fruitIndex = index;
        fruitData = data;

        var info = data.fruits[index];

        // Set collider size
        col.radius = info.radius;

        // Set visual
        transform.localScale = Vector3.one * info.radius * 2f;

        if (info.sprite != null)
            spriteRenderer.sprite = info.sprite;
        else
            spriteRenderer.color = info.color;

        // Physics settings
        rb.gravityScale = 1.5f;
        rb.linearDamping = 0.3f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        gameObject.name = info.fruitName;
    }

    public void Drop()
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        StartCoroutine(LandCheck());
    }

    IEnumerator LandCheck()
    {
        yield return new WaitForSeconds(landCheckDelay);
        while (!hasLanded)
        {
            yield return new WaitForSeconds(0.1f);
            if (rb.linearVelocity.magnitude < velocityThreshold)
                hasLanded = true;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isMerging) return;

        Fruit other = collision.gameObject.GetComponent<Fruit>();
        if (other == null || other.isMerging) return;
        if (other.fruitIndex != fruitIndex) return;

        // Prevent merging the largest fruit (last in the cycle)
        if (fruitIndex >= fruitData.fruits.Length - 1) return;

        // Only the fruit with the lower instance ID initiates to avoid double merge
        if (gameObject.GetInstanceID() < other.gameObject.GetInstanceID())
        {
            isMerging = true;
            other.isMerging = true;
            StartCoroutine(MergeWith(other));
        }
    }
 
    IEnumerator MergeWith(Fruit other)
    {
        // Brief pause for visual feedback
        yield return new WaitForSeconds(0.05f);

        Vector2 midPoint = (transform.position + other.transform.position) / 2f;

        // Spawn merged fruit
        GameManager.Instance.SpawnMergedFruit(fruitIndex + 1, midPoint);

        // Award points
        int earnedPoints = fruitData.fruits[fruitIndex + 1].points;
        GameManager.Instance.AddScore(earnedPoints);

        // Destroy both
        Destroy(other.gameObject);
        Destroy(gameObject);
    }

    public void MakeKinematic()
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        col.enabled = false;
    }
}*/