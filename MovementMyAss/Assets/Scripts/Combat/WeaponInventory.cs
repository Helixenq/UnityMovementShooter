using UnityEngine;

public class WeaponInventory : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Camera fpsCamera;
    [SerializeField] Transform weaponHolder;

    [Header("Weapons (children under holder)")]
    [SerializeField] HitScanWeapon[] weapons;

    [Header("Runtime")]
    [SerializeField] int currentIndex = 0;

    PlayerControls controls;
    HitScanWeapon Current => (weapons != null && weapons.Length > 0) ? weapons[currentIndex] : null;

    void Awake()
    {
        controls = new PlayerControls();
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Start()
    {
        if (fpsCamera == null) fpsCamera = Camera.main;

        if (weapons == null || weapons.Length == 0)
            weapons = weaponHolder.GetComponentsInChildren<HitScanWeapon>(true);

        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].gameObject.SetActive(i == currentIndex);
            weapons[i].InitAmmo();
        }
    }

    void Update()
    {
        if (Current == null || fpsCamera == null) return;

        bool fireHeld = controls.Player.Fire.IsPressed();
        bool firePressed = controls.Player.Fire.WasPressedThisFrame();

        // Fire mode handling
        if (Current.Definition.fireMode == FireMode.Auto)
        {
            if (fireHeld) Current.TryFire(fpsCamera, gameObject);
        }
        else
        {
            if (firePressed) Current.TryFire(fpsCamera, gameObject);
        }

        if (controls.Player.Reload.WasPressedThisFrame())
            Current.TryReload(this);

        // Switching (example: 1/2/3 keys – bind in Input Actions)
        if (controls.Player.Weapon1.WasPressedThisFrame()) EquipIndex(0);
        if (controls.Player.Weapon2.WasPressedThisFrame()) EquipIndex(1);
        if (controls.Player.Weapon3.WasPressedThisFrame()) EquipIndex(2);

        // Mouse wheel (optional): use a separate action or read Mouse.current.scroll
        if (controls.Player.NextWeapon.WasPressedThisFrame()) EquipNext();
        if (controls.Player.PrevWeapon.WasPressedThisFrame()) EquipPrev();

    }

    public void EquipIndex(int i)
    {
        if (weapons == null || weapons.Length == 0) return;
        if (i < 0 || i >= weapons.Length) return;
        if (i == currentIndex) return;

        weapons[currentIndex].CancelReload();

        weapons[currentIndex].gameObject.SetActive(false);
        currentIndex = i;
        weapons[currentIndex].gameObject.SetActive(true);
    }

    public (int mag, int reserve, string name) GetHUDData()
    {
        if (Current == null) return (0, 0, "");
        var ammo = Current.GetAmmo();
        return (ammo.mag, ammo.reserve, Current.Definition.weaponName);
    }
    public void EquipNext()
    {
        if (weapons == null || weapons.Length == 0) return;
        int next = (currentIndex + 1) % weapons.Length;
        EquipIndex(next);
    }

    public void EquipPrev()
    {
        if (weapons == null || weapons.Length == 0) return;
        int prev = (currentIndex - 1 + weapons.Length) % weapons.Length;
        EquipIndex(prev);
    }

}
