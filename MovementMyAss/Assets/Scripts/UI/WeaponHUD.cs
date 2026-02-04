using TMPro;
using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] WeaponInventory inventory;
    [SerializeField] DamageableVitals vitals;

    [Header("UI")]
    [SerializeField] TMP_Text weaponText;
    [SerializeField] TMP_Text ammoText;
    [SerializeField] TMP_Text vitalsText;

    void Start()
    {
        if (inventory == null) inventory = FindFirstObjectByType<WeaponInventory>();
        if (vitals == null) vitals = FindFirstObjectByType<DamageableVitals>();
    }

    void Update()
    {
        if (inventory != null)
        {
            var hud = inventory.GetHUDData();
            if (weaponText) weaponText.text = hud.name;
            if (ammoText) ammoText.text = $"{hud.mag} / {hud.reserve}";
        }

        if (vitals != null && vitalsText != null)
        {
            // Assuming DamageableVitals exposes current values. If not, tell me what fields it has.
            vitalsText.text = $"HP: {vitals.Health}  ARM: {vitals.Armor}";
        }
    }
}
