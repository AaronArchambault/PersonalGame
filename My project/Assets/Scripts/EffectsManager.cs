using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  EffectsManager
//  Central hub for visual and audio effects.
//  Uses a per-type ParticleSystem pool so effects are cheap to fire.
// ─────────────────────────────────────────────────────────────────────────────
public class EffectsManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Particles — index matches GemType enum")]
    [Tooltip("Assign one ParticleSystem prefab per gem colour (Red, Blue, Green, Yellow, Purple, Wild)")]
    public ParticleSystem[] gemDestroyParticles;

    [Header("Obstacle Effects")]
    public ParticleSystem iceBreakParticle;
    public ParticleSystem lockBreakParticle;
    public ParticleSystem chocolateClearParticle;

    [Header("Special Effects")]
    public ParticleSystem stripedEffectH;   // horizontal laser sweep
    public ParticleSystem stripedEffectV;   // vertical laser sweep
    public ParticleSystem bombEffect;
    public ParticleSystem colorBombEffect;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   matchSound;
    public AudioClip   specialSound;
    public AudioClip   obstacleBreakSound;
    public AudioClip   reshuffleSound;
    public AudioClip   failSound;
    public AudioClip   winSound;

    // ── Pool ──────────────────────────────────────────────────────────────────
    private Dictionary<int, Queue<ParticleSystem>> pool = new();

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────
    public void PlayDestroyEffect(Vector3 worldPos, GemType type)
    {
        int idx = Mathf.Clamp((int)type, 0, gemDestroyParticles.Length - 1);
        if (gemDestroyParticles == null || idx >= gemDestroyParticles.Length) return;
        var prefab = gemDestroyParticles[idx];
        if (prefab == null) return;

        var ps = GetFromPool(idx, prefab);
        ps.transform.position = worldPos;
        ps.Play();
        PlaySfx(matchSound);
    }

    public void PlayObstacleClearEffect(Vector3 worldPos)
    {
        PlayOne(iceBreakParticle, worldPos);
        PlaySfx(obstacleBreakSound);
    }

    public void PlayStripedH(Vector3 worldPos) => PlayOne(stripedEffectH, worldPos, PlaySfxSpecial);
    public void PlayStripedV(Vector3 worldPos) => PlayOne(stripedEffectV, worldPos, PlaySfxSpecial);
    public void PlayBomb    (Vector3 worldPos) => PlayOne(bombEffect,      worldPos, PlaySfxSpecial);
    public void PlayColorBomb(Vector3 worldPos)=> PlayOne(colorBombEffect, worldPos, PlaySfxSpecial);

    public void PlayWin()     => PlaySfx(winSound);
    public void PlayFail()    => PlaySfx(failSound);
    public void PlayReshuffle()=>PlaySfx(reshuffleSound);

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    void PlayOne(ParticleSystem prefab, Vector3 pos, System.Action sfxCallback = null)
    {
        if (prefab == null) return;
        var ps = Instantiate(prefab, pos, Quaternion.identity, transform);
        ps.Play();
        Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 0.5f);
        sfxCallback?.Invoke();
    }

    void PlaySfxSpecial() => PlaySfx(specialSound);

    void PlaySfx(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    // ── Particle pool ─────────────────────────────────────────────────────────
    ParticleSystem GetFromPool(int idx, ParticleSystem prefab)
    {
        if (!pool.ContainsKey(idx)) pool[idx] = new Queue<ParticleSystem>();

        // Recycle any finished instance
        while (pool[idx].Count > 0)
        {
            var ps = pool[idx].Peek();
            if (ps == null) { pool[idx].Dequeue(); continue; }   // destroyed
            if (!ps.isPlaying) { pool[idx].Dequeue(); pool[idx].Enqueue(ps); return ps; }
            break;
        }

        // Spawn new
        var newPs = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        newPs.Stop();
        pool[idx].Enqueue(newPs);
        return newPs;
    }
}