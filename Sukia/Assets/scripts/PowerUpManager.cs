using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public enum PowerUpType
{
    Bomb, Freeze, Shrink, Wildcard, GravityFlip, ScoreMultiplier
}

[System.Serializable]
public class PowerUp
{
    public PowerUpType type;
    public string      label;
    public string      description;
    public Color       color;
}

public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("Definitions")]
    public PowerUp[] powerUpDefs;

    [Header("UI — assign the PowerUpPanel RectTransform")]
    public RectTransform slotContainer; // the PowerUpPanel

    [Header("Settings")]
    public int   maxSlots             = 3;
    public float bombRadius           = 1.5f;
    public float freezeDuration       = 3f;
    public float gravityFlipDuration  = 3f;
    public int   multiplierMerges     = 5;

    // State
    public bool  isWildcardActive     { get; private set; }
    public bool  isShrinkActive       { get; private set; }
    public bool  isFreezeActive       { get; private set; }
    public int   multiplierMergesLeft { get; private set; }
    public float scoreMultiplier      => multiplierMergesLeft > 0 ? 2f : 1f;

    private List<PowerUpType>  inventory   = new List<PowerUpType>();
    private List<GameObject>   slotObjects = new List<GameObject>();

    // Slot colors
    private static readonly Color COL_EMPTY  = new Color(0.15f, 0.15f, 0.20f, 0.85f);
    private static readonly Color COL_BOMB   = new Color(0.80f, 0.20f, 0.20f);
    private static readonly Color COL_FREEZE = new Color(0.20f, 0.55f, 0.90f);
    private static readonly Color COL_SHRINK = new Color(0.85f, 0.75f, 0.10f);
    private static readonly Color COL_WILD   = new Color(0.70f, 0.30f, 0.90f);
    private static readonly Color COL_GRAV   = new Color(0.20f, 0.75f, 0.60f);
    private static readonly Color COL_MULTI  = new Color(0.15f, 0.70f, 0.30f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        SetupContainerBackground();
        BuildSlots();
    }

    // ── Setup the panel background ────────────────────────────────────────────

    void SetupContainerBackground()
    {
        if (slotContainer == null) return;
        Image bg = slotContainer.GetComponent<Image>();
        if (bg == null) bg = slotContainer.gameObject.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.10f, 0.85f);

        // Remove any layout group that might fight our manual placement
        HorizontalLayoutGroup hlg = slotContainer.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) Destroy(hlg);
        VerticalLayoutGroup vlg = slotContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) Destroy(vlg);
    }

    // ── Build slot buttons in code ────────────────────────────────────────────

    void BuildSlots()
    {
        if (slotContainer == null) return;

        // Clear old slots
        foreach (Transform child in slotContainer)
            Destroy(child.gameObject);
        slotObjects.Clear();

        // Header label
        GameObject header = new GameObject("Header");
        header.transform.SetParent(slotContainer, false);
        RectTransform hrt = header.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.78f);
        hrt.anchorMax = new Vector2(1f, 1.00f);
        hrt.offsetMin = Vector2.zero;
        hrt.offsetMax = Vector2.zero;
        TextMeshProUGUI htmp = header.AddComponent<TextMeshProUGUI>();
        htmp.text      = "Power-ups";
        htmp.fontSize  = 14;
        htmp.color     = new Color(0.8f, 0.8f, 0.9f);
        htmp.alignment = TextAlignmentOptions.Center;

        // Slot buttons — evenly spaced across the panel
        float slotWidth  = 1f / maxSlots;
        float padding    = 0.02f;

        for (int i = 0; i < maxSlots; i++)
        {
            float xMin = i * slotWidth + padding;
            float xMax = (i + 1) * slotWidth - padding;

            GameObject slot = new GameObject("Slot_" + i);
            slot.transform.SetParent(slotContainer, false);

            RectTransform rt = slot.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0.04f);
            rt.anchorMax = new Vector2(xMax, 0.76f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img  = slot.AddComponent<Image>();
            img.color  = COL_EMPTY;

            Button btn = slot.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable  = false;

            ColorBlock cb       = btn.colors;
            cb.normalColor      = COL_EMPTY;
            cb.highlightedColor = COL_EMPTY * 1.3f;
            cb.pressedColor     = COL_EMPTY * 0.7f;
            cb.colorMultiplier  = 1f;
            btn.colors          = cb;

            int captured = i;
            btn.onClick.AddListener(() => ActivatePowerUp(captured));

            // Label inside slot
            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(slot.transform, false);
            RectTransform lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0.55f);
            lrt.anchorMax = new Vector2(0.95f, 0.95f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            TextMeshProUGUI ltmp = labelGo.AddComponent<TextMeshProUGUI>();
            ltmp.text      = "-";
            ltmp.fontSize  = 18;
            ltmp.color     = new Color(0.5f, 0.5f, 0.55f);
            ltmp.alignment = TextAlignmentOptions.Center;

            // Sub-label (power-up name)
            GameObject subGo = new GameObject("Sub");
            subGo.transform.SetParent(slot.transform, false);
            RectTransform srt = subGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.02f, 0.05f);
            srt.anchorMax = new Vector2(0.98f, 0.54f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            TextMeshProUGUI stmp = subGo.AddComponent<TextMeshProUGUI>();
            stmp.text      = "empty";
            stmp.fontSize  = 10;
            stmp.color     = new Color(0.4f, 0.4f, 0.45f);
            stmp.alignment = TextAlignmentOptions.Center;

            slotObjects.Add(slot);
        }

        RefreshSlotUI();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void OfferRandomPowerUp()
    {
        if (!enabled || powerUpDefs == null || powerUpDefs.Length == 0) return;
        if (inventory.Count >= maxSlots) return;
        AddToInventory(powerUpDefs[Random.Range(0, powerUpDefs.Length)].type);
    }

    public void AddToInventory(PowerUpType type)
    {
        if (inventory.Count >= maxSlots) return;
        inventory.Add(type);
        RefreshSlotUI();
    }

    public void ActivatePowerUp(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventory.Count) return;
        PowerUpType type = inventory[slotIndex];
        inventory.RemoveAt(slotIndex);
        RefreshSlotUI();

        switch (type)
        {
            case PowerUpType.Bomb:            StartCoroutine(DoBomb());          break;
            case PowerUpType.Freeze:          StartCoroutine(DoFreeze());        break;
            case PowerUpType.Shrink:          isShrinkActive = true;             break;
            case PowerUpType.Wildcard:        isWildcardActive = true;           break;
            case PowerUpType.GravityFlip:     StartCoroutine(DoGravityFlip());   break;
            case PowerUpType.ScoreMultiplier: multiplierMergesLeft = multiplierMerges; break;
        }
    }

    public void ConsumeWildcard()  => isWildcardActive = false;
    public void ConsumeShrink()    => isShrinkActive   = false;
    public void ConsumeMultiplierCharge()
    { if (multiplierMergesLeft > 0) multiplierMergesLeft--; }

    // ── Coroutines ────────────────────────────────────────────────────────────

    IEnumerator DoBomb()
    {
        bool waiting = true;
        while (waiting)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 screen   = Mouse.current.position.ReadValue();
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(screen.x, screen.y, 0));

                Fruit[] all = FindObjectsByType<Fruit>(FindObjectsSortMode.None);
                foreach (Fruit f in all)
                    if (Vector2.Distance(f.transform.position, worldPos) <= bombRadius)
                    { GameManager.Instance.AddScore(f.fruitIndex * 2); Destroy(f.gameObject); }

                waiting = false;
            }
            yield return null;
        }
    }

    IEnumerator DoFreeze()
    {
        isFreezeActive = true;
        var rbs = new List<Rigidbody2D>();
        foreach (Fruit f in FindObjectsByType<Fruit>(FindObjectsSortMode.None))
        {
            Rigidbody2D rb = f.GetComponent<Rigidbody2D>();
            if (rb) { rb.bodyType = RigidbodyType2D.Static; rbs.Add(rb); }
        }
        yield return new WaitForSeconds(freezeDuration);
        foreach (var rb in rbs)
            if (rb) rb.bodyType = RigidbodyType2D.Dynamic;
        isFreezeActive = false;
    }

    IEnumerator DoGravityFlip()
    {
        Physics2D.gravity = new Vector2(0f,  Mathf.Abs(Physics2D.gravity.y));
        yield return new WaitForSeconds(gravityFlipDuration);
        Physics2D.gravity = new Vector2(0f, -Mathf.Abs(Physics2D.gravity.y));
    }

    // ── Refresh slot visuals ──────────────────────────────────────────────────

    void RefreshSlotUI()
    {
        for (int i = 0; i < slotObjects.Count; i++)
        {
            if (slotObjects[i] == null) continue;

            bool    hasItem  = i < inventory.Count;
            Image   img      = slotObjects[i].GetComponent<Image>();
            Button  btn      = slotObjects[i].GetComponent<Button>();
            var     texts    = slotObjects[i].GetComponentsInChildren<TextMeshProUGUI>();

            Color slotColor = hasItem ? GetColor(inventory[i]) : COL_EMPTY;
            if (img) img.color = slotColor;
            if (btn)
            {
                btn.interactable = hasItem;
                ColorBlock cb       = btn.colors;
                cb.normalColor      = slotColor;
                cb.highlightedColor = slotColor * 1.3f;
                cb.pressedColor     = slotColor * 0.7f;
                btn.colors          = cb;
            }

            if (texts.Length > 0)
            {
                texts[0].text  = hasItem ? GetEmoji(inventory[i]) : "-";
                texts[0].color = hasItem ? Color.white : new Color(0.4f, 0.4f, 0.45f);
            }
            if (texts.Length > 1)
            {
                texts[1].text  = hasItem ? GetLabel(inventory[i]) : "empty";
                texts[1].color = hasItem ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.35f, 0.35f, 0.40f);
            }
        }
    }

    Color  GetColor(PowerUpType t) => t switch {
        PowerUpType.Bomb            => COL_BOMB,
        PowerUpType.Freeze          => COL_FREEZE,
        PowerUpType.Shrink          => COL_SHRINK,
        PowerUpType.Wildcard        => COL_WILD,
        PowerUpType.GravityFlip     => COL_GRAV,
        PowerUpType.ScoreMultiplier => COL_MULTI,
        _                           => COL_EMPTY
    };

    string GetEmoji(PowerUpType t) => t switch {
        PowerUpType.Bomb            => "B",
        PowerUpType.Freeze          => "Fr",
        PowerUpType.Shrink          => "Sh",
        PowerUpType.Wildcard        => "W",
        PowerUpType.GravityFlip     => "Gv",
        PowerUpType.ScoreMultiplier => "2x",
        _                           => "?"
    };

    string GetLabel(PowerUpType t) => t switch {
        PowerUpType.Bomb            => "Bomb",
        PowerUpType.Freeze          => "Freeze",
        PowerUpType.Shrink          => "Shrink",
        PowerUpType.Wildcard        => "Wild",
        PowerUpType.GravityFlip     => "Flip",
        PowerUpType.ScoreMultiplier => "Multi",
        _                           => ""
    };
}















/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public enum PowerUpType
{
    Bomb,           // Destroys all fruits in a radius
    Freeze,         // Pauses all physics for a few seconds
    Shrink,         // Next fruit is one tier smaller
    Wildcard,       // Next fruit merges with ANY other fruit (one merge)
    GravityFlip,    // Reverses gravity briefly
    ScoreMultiplier // 2× score for next N merges
}

[System.Serializable]
public class PowerUp
{
    public PowerUpType type;
    public string      label;
    public string      description;
    public Color       color;
}

public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("Definitions")]
    public PowerUp[] powerUpDefs; // configure in Inspector

    [Header("UI")]
    public Transform  slotContainer;   // horizontal layout holding slots
    public GameObject slotPrefab;      // Button + icon label
    public int        maxSlots = 3;

    [Header("Settings")]
    public float bombRadius        = 1.5f;
    public float freezeDuration    = 3f;
    public float gravityFlipDuration = 3f;
    public int   multiplierMerges  = 5;

    // State
    private List<PowerUpType> inventory = new List<PowerUpType>();
    private List<Button>      slotButtons = new List<Button>();

    public bool  isWildcardActive     { get; private set; }
    public bool  isShrinkActive       { get; private set; }
    public bool  isFreezeActive       { get; private set; }
    public int   multiplierMergesLeft { get; private set; }
    public float scoreMultiplier      => multiplierMergesLeft > 0 ? 2f : 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildSlotUI();
    }

    // ─── Offer a random power-up to the player ────────────────────────────────

    public void OfferRandomPowerUp()
    {
        if (!enabled || powerUpDefs == null || powerUpDefs.Length == 0) return;
        if (inventory.Count >= maxSlots) return;

        PowerUpType offered = powerUpDefs[Random.Range(0, powerUpDefs.Length)].type;
        AddToInventory(offered);
    }

    public void AddToInventory(PowerUpType type)
    {
        if (inventory.Count >= maxSlots) return;
        inventory.Add(type);
        RefreshSlotUI();
    }

    // ─── Activate ─────────────────────────────────────────────────────────────

    public void ActivatePowerUp(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventory.Count) return;

        PowerUpType type = inventory[slotIndex];
        inventory.RemoveAt(slotIndex);
        RefreshSlotUI();

        switch (type)
        {
            case PowerUpType.Bomb:           StartCoroutine(DoBomb());           break;
            case PowerUpType.Freeze:         StartCoroutine(DoFreeze());         break;
            case PowerUpType.Shrink:         isShrinkActive = true;              break;
            case PowerUpType.Wildcard:       isWildcardActive = true;            break;
            case PowerUpType.GravityFlip:    StartCoroutine(DoGravityFlip());    break;
            case PowerUpType.ScoreMultiplier: multiplierMergesLeft = multiplierMerges; break;
        }
    }

    // Called by Fruit.cs after a wildcard merge fires
    public void ConsumeWildcard() => isWildcardActive = false;

    // Called by Fruit.cs after shrink is applied
    public void ConsumeShrink()   => isShrinkActive = false;

    // Called by GameManager after each merge when multiplier is active
    public void ConsumeMultiplierCharge()
    {
        if (multiplierMergesLeft > 0) multiplierMergesLeft--;
    }

    // ─── Coroutines ───────────────────────────────────────────────────────────

    IEnumerator DoBomb()
    {
        // Wait for player to click a target position
        yield return StartCoroutine(WaitForClickThenExplode());
    }

    IEnumerator WaitForClickThenExplode()
    {
        // Visual hint
        Debug.Log("[PowerUp] Bomb: click to place");

        bool waiting = true;
        Vector2 worldPos = Vector2.zero;

        // Wait for next click
        while (waiting)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector3 screen = Mouse.current.position.ReadValue();
                worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0));
                waiting = false;
            }
            yield return null;
        }

        // Destroy all fruits within bombRadius
        Fruit[] allFruits = FindObjectsByType<Fruit>(FindObjectsSortMode.None);
        foreach (Fruit f in allFruits)
        {
            if (Vector2.Distance(f.transform.position, worldPos) <= bombRadius)
            {
                GameManager.Instance.AddScore(f.fruitIndex * 2); // bonus for cleared fruits
                Destroy(f.gameObject);
            }
        }
    }

    IEnumerator DoFreeze()
    {
        isFreezeActive = true;
        Fruit[] allFruits = FindObjectsByType<Fruit>(FindObjectsSortMode.None);
        List<Rigidbody2D> rbs = new List<Rigidbody2D>();

        foreach (Fruit f in allFruits)
        {
            Rigidbody2D rb = f.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.bodyType = RigidbodyType2D.Static; rbs.Add(rb); }
        }

        yield return new WaitForSeconds(freezeDuration);

        foreach (Rigidbody2D rb in rbs)
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;

        isFreezeActive = false;
    }

    IEnumerator DoGravityFlip()
    {
        Physics2D.gravity = new Vector2(0f, Mathf.Abs(Physics2D.gravity.y));
        yield return new WaitForSeconds(gravityFlipDuration);
        Physics2D.gravity = new Vector2(0f, -Mathf.Abs(Physics2D.gravity.y));
    }

    // ─── UI ───────────────────────────────────────────────────────────────────

    void BuildSlotUI()
    {
        if (slotContainer == null || slotPrefab == null) return;

        foreach (Transform c in slotContainer) Destroy(c.gameObject);
        slotButtons.Clear();

        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotContainer);
            Button btn = slot.GetComponent<Button>();
            int captured = i;
            if (btn != null)
                btn.onClick.AddListener(() => ActivatePowerUp(captured));
            slotButtons.Add(btn);
        }

        RefreshSlotUI();
    }

    void RefreshSlotUI()
    {
        for (int i = 0; i < slotButtons.Count; i++)
        {
            if (slotButtons[i] == null) continue;

            bool hasItem = i < inventory.Count;
            slotButtons[i].interactable = hasItem;

            TextMeshProUGUI label = slotButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = hasItem ? GetDef(inventory[i])?.label ?? "?" : "-";

            Image img = slotButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = hasItem ? (GetDef(inventory[i])?.color ?? Color.white) : new Color(0.3f, 0.3f, 0.3f, 0.4f);
        }
    }

    PowerUp GetDef(PowerUpType type)
    {
        if (powerUpDefs == null) return null;
        foreach (var p in powerUpDefs)
            if (p.type == type) return p;
        return null;
    }
}*/