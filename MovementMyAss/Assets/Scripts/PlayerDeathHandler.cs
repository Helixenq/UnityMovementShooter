using System.Collections;
using UnityEngine;

public class PlayerDeathHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] DamageableVitals vitals;
    [SerializeField] MonoBehaviour[] disableOnDeath; // drag PlayerMovement, WeaponInventory, etc
    [SerializeField] Transform respawnPoint;

    [Header("UI (optional)")]
    [SerializeField] CanvasGroup deathUI; // optional “You Died” panel

    [Header("Respawn")]
    [SerializeField] float respawnDelay = 2f;

    bool dead;

    void Awake()
    {
        if (vitals == null) vitals = GetComponent<DamageableVitals>();
        vitals.OnDied += OnDied;
        if (deathUI != null) SetDeathUI(false);
    }

    void OnDestroy()
    {
        if (vitals != null) vitals.OnDied -= OnDied;
    }

    void OnDied(GameObject instigator)
    {
        if (dead) return;
        dead = true;

        // disable movement/weapons/etc
        foreach (var mb in disableOnDeath)
            if (mb != null) mb.enabled = false;

        if (deathUI != null) SetDeathUI(true);

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // move + stop velocity
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (respawnPoint != null)
            transform.position = respawnPoint.position;

        vitals.ResetVitals();

        // re-enable controls
        foreach (var mb in disableOnDeath)
            if (mb != null) mb.enabled = true;

        if (deathUI != null) SetDeathUI(false);

        dead = false;
    }

    void SetDeathUI(bool on)
    {
        deathUI.alpha = on ? 1f : 0f;
        deathUI.interactable = on;
        deathUI.blocksRaycasts = on;
    }
}
