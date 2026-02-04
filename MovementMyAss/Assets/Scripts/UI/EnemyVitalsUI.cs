using UnityEngine;
using UnityEngine.UI;

public class EnemyVitalsUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] DamageableVitals vitals;
    [SerializeField] Image healthFill;
    [SerializeField] Image armorFill;

    [Header("Optional")]
    [SerializeField] Transform billboardTarget; // usually player camera

    void Awake()
    {
        if (vitals == null) vitals = GetComponentInParent<DamageableVitals>();
    }

    void OnEnable()
    {
        if (vitals == null) return;
        vitals.OnVitalsChanged += OnVitalsChanged;
        // IMPORTANT: UI usually subscribes after vitals.Awake(), so do an initial refresh:
        OnVitalsChanged(vitals.Health, vitals.Armor);
    }

    void OnDisable()
    {
        if (vitals == null) return;
        vitals.OnVitalsChanged -= OnVitalsChanged;
    }

    void LateUpdate()
    {
        if (billboardTarget != null)
        {
            // Face camera
            Vector3 dir = transform.position - billboardTarget.position;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void OnVitalsChanged(float health, float armor)
    {
        if (healthFill != null)
            healthFill.fillAmount = (vitals.MaxHealth <= 0f) ? 0f : Mathf.Clamp01(health / vitals.MaxHealth);

        if (armorFill != null)
            armorFill.fillAmount = (vitals.MaxArmor <= 0f) ? 0f : Mathf.Clamp01(armor / vitals.MaxArmor);

        // Optional: hide when dead
        // gameObject.SetActive(health > 0f);
    }
}
