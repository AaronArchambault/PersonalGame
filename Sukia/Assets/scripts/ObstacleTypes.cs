using System.Collections;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ObstacleBase — shared logic for all obstacle types
// ─────────────────────────────────────────────────────────────────────────────
public abstract class ObstacleBase : MonoBehaviour
{
    [Header("Obstacle Base")]
    public Color obstacleColor = new Color(0.35f, 0.35f, 0.4f);

    protected virtual void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = obstacleColor;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// StaticBlock — unmovable rectangular blocker
// ─────────────────────────────────────────────────────────────────────────────
public class StaticBlock : ObstacleBase
{
    // No extra behaviour needed — static Rigidbody2D + BoxCollider2D handles it
}

// ─────────────────────────────────────────────────────────────────────────────
// MovingPlatform — slides back and forth, carries fruits
// ─────────────────────────────────────────────────────────────────────────────
public class MovingPlatform : ObstacleBase
{
    public float moveSpeed = 1.5f;
    public float moveRange = 1.5f;
    public bool  horizontal = true;

    private Vector3 origin;
    private Rigidbody2D rb;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
        origin = transform.position;
    }

    void FixedUpdate()
    {
        float offset = Mathf.Sin(Time.time * moveSpeed) * moveRange;
        Vector3 target = horizontal
            ? origin + new Vector3(offset, 0f, 0f)
            : origin + new Vector3(0f, offset, 0f);

        if (rb != null) rb.MovePosition(target);
        else transform.position = target;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// StickyZone — trigger zone that dampens fruit velocity (like mud)
// ─────────────────────────────────────────────────────────────────────────────
public class StickyZone : ObstacleBase
{
    public float dragMultiplier = 8f;

    protected override void Awake()
    {
        base.Awake();
        obstacleColor = new Color(0.6f, 0.45f, 0.2f, 0.45f);
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = obstacleColor;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;
        rb.linearVelocity *= 1f - (dragMultiplier * Time.fixedDeltaTime);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Bumper — circular spring that bounces fruits away
// ─────────────────────────────────────────────────────────────────────────────
public class Bumper : ObstacleBase
{
    public float bounceForce = 6f;

    protected override void Awake()
    {
        base.Awake();
        obstacleColor = new Color(0.9f, 0.3f, 0.6f);
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) { sr.color = obstacleColor; sr.sortingOrder = 1; }

        // Make sure we have a trigger for detection + a collider for physics
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = false; // solid — normal collision handles bounce
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D rb = collision.rigidbody;
        if (rb == null) return;

        Vector2 dir = ((Vector2)collision.transform.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * bounceForce;

        // Quick visual flash
        StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color orig = sr.color;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        sr.color = orig;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DividerWall — vertical or horizontal partial wall splitting the container
// ─────────────────────────────────────────────────────────────────────────────
public class DividerWall : ObstacleBase
{
    // Static — same as StaticBlock but with a different visual intent.
    // Just mark it appropriately in the Editor.
}

// ─────────────────────────────────────────────────────────────────────────────
// GravityWell — pulls nearby fruits toward its centre
// ─────────────────────────────────────────────────────────────────────────────
public class GravityWell : ObstacleBase
{
    public float pullRadius = 2f;
    public float pullForce  = 4f;

    protected override void Awake()
    {
        base.Awake();
        obstacleColor = new Color(0.4f, 0.15f, 0.7f, 0.5f);
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = obstacleColor;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = pullRadius;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;

        Vector2 dir = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
        float dist  = Vector2.Distance(transform.position, other.transform.position);
        float force = pullForce * (1f - dist / pullRadius); // stronger near centre
        rb.AddForce(dir * force);
    }
}