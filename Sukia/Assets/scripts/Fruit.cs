using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Fruit : MonoBehaviour
{
    public int  fruitIndex { get; private set; }
    public bool hasLanded  { get; private set; } = false;
    public bool isMerging  { get; private set; } = false;

    private Rigidbody2D    rb;
    private CircleCollider2D col;
    private SpriteRenderer   spriteRenderer;
    private FruitData        fruitData;

    private float landCheckDelay  = 0.15f;
    private float velocityThreshold = 0.05f;

    void Awake()
    {
        rb             = GetComponent<Rigidbody2D>();
        col            = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(int index, FruitData data)
    {
        fruitIndex = index;
        fruitData  = data;

        if (rb             == null) rb             = GetComponent<Rigidbody2D>();
        if (col            == null) col            = GetComponent<CircleCollider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        var info = data.fruits[index];

        // Scale drives visual size; col.radius = 0.5 in local space = correct world radius
        float diameter = info.radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);
        col.radius = 0.5f;

        // Sprite
        if (info.sprite != null)
            spriteRenderer.sprite = info.sprite;
        else
            spriteRenderer.sprite = FruitSpriteGenerator.CreateCircleSprite(info.color, Color.white * 0.6f);

        spriteRenderer.color = Color.white;

        // Physics
        rb.gravityScale          = GameManager.Instance?.activeLevelFruitGravity ?? 1.5f;
        rb.linearDamping         = 0.3f;
        rb.angularDamping        = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation         = RigidbodyInterpolation2D.Interpolate;

        gameObject.name = info.fruitName;
    }

    public void Drop()
    {
        if (col == null) col = GetComponent<CircleCollider2D>();
        gameObject.layer = LayerMask.NameToLayer("Default");
        StartCoroutine(EnableAndDrop());
    }

    IEnumerator EnableAndDrop()
    {
        yield return new WaitForFixedUpdate();
        col.enabled   = true;
        rb.bodyType   = RigidbodyType2D.Dynamic;
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

        bool classicMatch  = other.fruitIndex == fruitIndex;
        bool wildcardMatch = false;

        // Wildcard: merge with any fruit (pick the higher-tier result)
        if (!classicMatch)
        {
            bool myWild    = PowerUpManager.Instance != null && PowerUpManager.Instance.isWildcardActive;
            wildcardMatch  = myWild;
        }

        if (!classicMatch && !wildcardMatch) return;
        if (fruitIndex >= fruitData.fruits.Length - 1 && classicMatch) return;

        if (gameObject.GetInstanceID() < other.gameObject.GetInstanceID())
        {
            isMerging = true;
            other.isMerging = true;
            int mergeResult = wildcardMatch
                ? Mathf.Max(fruitIndex, other.fruitIndex) + 1
                : fruitIndex + 1;

            if (wildcardMatch && PowerUpManager.Instance != null)
                PowerUpManager.Instance.ConsumeWildcard();

            StartCoroutine(MergeWith(other, mergeResult));
        }
    }

    IEnumerator MergeWith(Fruit other, int resultIndex)
    {
        yield return new WaitForSeconds(0.05f);

        resultIndex = Mathf.Min(resultIndex, fruitData.fruits.Length - 1);
        Vector2 midPoint = (transform.position + other.transform.position) / 2f;

        GameManager.Instance.SpawnMergedFruit(resultIndex, midPoint);
        GameManager.Instance.AddScore(fruitData.fruits[resultIndex].points);
        MergeEffect.Spawn(midPoint, fruitData.fruits[resultIndex].color);

        Destroy(other.gameObject);
        Destroy(gameObject);
    }

    public void MakeKinematic()
    {
        if (rb  == null) rb  = GetComponent<Rigidbody2D>();
        if (col == null) col = GetComponent<CircleCollider2D>();

        gameObject.layer  = LayerMask.NameToLayer("Held");
        rb.bodyType       = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        col.enabled       = false;
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

        if (rb == null)             rb = GetComponent<Rigidbody2D>();
        if (col == null)            col = GetComponent<CircleCollider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        var info = data.fruits[index];

        // ── Keep transform scale at exactly 1,1,1 always ──────────────────────
        // If scale is anything other than 1, Unity multiplies it into the
        // collider radius, making the physics shape a different size than intended.
        transform.localScale = Vector3.one;

        // ── Collider ──────────────────────────────────────────────────────────
        // With scale = 1, col.radius IS the world-space radius. Simple.
        col.radius = info.radius;

        // ── Sprite ────────────────────────────────────────────────────────────
        // A default Unity sprite is 1 unit wide at scale 1.
        // We need it to be (radius * 2) units wide, so scale the GO to diameter.
        // Because we reset scale to 1 above first, this is always correct.
        float diameter = info.radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);

        // With scale = diameter, the collider radius gets multiplied too!
        // So we must SET col.radius AFTER setting the scale, dividing by scale
        // so the final world radius = (info.radius / diameter) * diameter = info.radius
        col.radius = 0.5f; // 0.5 local units * diameter scale = info.radius world units

        // Generate circle sprite if none assigned
        if (info.sprite != null)
            spriteRenderer.sprite = info.sprite;
        else
            spriteRenderer.sprite = FruitSpriteGenerator.CreateCircleSprite(info.color, Color.white * 0.6f);

        spriteRenderer.color = Color.white; // tint resets so the generated sprite colour shows correctly

        // ── Physics ───────────────────────────────────────────────────────────
        rb.gravityScale = 2f;
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

        StartCoroutine(EnableAndDrop());
    }

    IEnumerator EnableAndDrop()
    {
        // Wait one fixed update so the layer change propagates before enabling collision
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

        if (fruitIndex >= fruitData.fruits.Length - 1) return;

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

        GameManager.Instance.SpawnMergedFruit(fruitIndex + 1, midPoint);

        int earnedPoints = fruitData.fruits[fruitIndex + 1].points;
        GameManager.Instance.AddScore(earnedPoints);

        MergeEffect.Spawn(midPoint, fruitData.fruits[fruitIndex + 1].color);

        Destroy(other.gameObject);
        Destroy(gameObject);
    }

    public void MakeKinematic()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (col == null) col = GetComponent<CircleCollider2D>();

        gameObject.layer = LayerMask.NameToLayer("Held");

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        col.enabled = false;
    }
}*/


