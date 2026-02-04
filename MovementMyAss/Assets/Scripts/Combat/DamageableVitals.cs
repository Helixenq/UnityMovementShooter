using System;
using UnityEngine;

public class DamageableVitals : MonoBehaviour, IDamageable
{
    [Header("Vitals")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float maxArmor = 50f;

    [Header("Runtime")]
    [SerializeField] float health;
    [SerializeField] float armor;

    public event Action<float, float> OnVitalsChanged; // health, armor
    public event Action<GameObject> OnDied;

    public bool IsDead { get; private set; }

    void Awake()
    {
        health = maxHealth;
        armor = maxArmor;
        IsDead = false;
        OnVitalsChanged?.Invoke(health, armor);
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f) return;
        health = Mathf.Min(maxHealth, health + amount);
        OnVitalsChanged?.Invoke(health, armor);
    }

    public void AddArmor(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f) return;
        armor = Mathf.Min(maxArmor, armor + amount);
        OnVitalsChanged?.Invoke(health, armor);
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator)
    {
        if (IsDead) return;
        if (amount <= 0f) return;

        float remaining = amount;

        // Armor absorbs first
        if (armor > 0f)
        {
            float absorbed = Mathf.Min(armor, remaining);
            armor -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f)
        {
            health -= remaining;
        }

        OnVitalsChanged?.Invoke(health, armor);

        if (health <= 0f)
        {
            IsDead = true;
            health = 0f;
            OnVitalsChanged?.Invoke(health, armor);
            OnDied?.Invoke(instigator);
        }
        health = Mathf.Max(0f, health);
        armor = Mathf.Max(0f, armor);

    }
    public void ResetVitals()
    {
        health = maxHealth;
        armor = maxArmor;
        OnVitalsChanged?.Invoke(health, armor);
    }


    public float Health => health;
    public float Armor => armor;
    public float MaxHealth => maxHealth;
    public float MaxArmor => maxArmor;
}
