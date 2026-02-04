using UnityEngine;

public class EnemyDeath : MonoBehaviour
{
    [SerializeField] DamageableVitals vitals;
    [SerializeField] float destroyDelay = 0.1f;
    [SerializeField] GameObject deathVfxPrefab;

    void Awake()
    {
        if (vitals == null) vitals = GetComponent<DamageableVitals>();
        vitals.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (vitals != null) vitals.OnDied -= HandleDied;
    }

    void HandleDied(GameObject instigator)
    {
        if (deathVfxPrefab != null)
            Instantiate(deathVfxPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);

        Destroy(gameObject, destroyDelay);
    }
}
