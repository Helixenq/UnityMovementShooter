using UnityEngine;

public enum FireMode { Semi, Auto }

[CreateAssetMenu(menuName = "FPS/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    public string weaponName = "Rifle";

    [Header("Firing")]
    public FireMode fireMode = FireMode.Auto;
    public float damage = 20f;
    public float range = 200f;
    public float fireRate = 10f; // shots per second
    public float spreadDegrees = 1.0f;

    [Header("Ammo")]
    public int magazineSize = 30;
    public int maxReserve = 120;
    public float reloadTime = 1.7f;

    [Header("Recoil (simple)")]
    public float recoilKick = 1.2f;

    [Header("FX")]
    public AudioClip shotSound;
    public ParticleSystem muzzleFlashPrefab;
    public GameObject impactPrefab;
}
