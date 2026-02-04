using System.Collections;
using UnityEngine;

public class HitScanWeapon : MonoBehaviour
{
    [SerializeField] WeaponDefinition def;
    [SerializeField] LayerMask hitMask = ~0; // everything by default

    [Header("Refs")]
    [SerializeField] Transform muzzle;
    [SerializeField] AudioSource audioSource;

    [Header("FX")]
    [SerializeField] ParticleSystem muzzleFlash;
    [SerializeField] TrailRenderer tracerPrefab;
    [SerializeField] float tracerSpeed = 250f;


    [Header("Runtime")]
    [SerializeField] int ammoInMag;
    [SerializeField] int reserveAmmo;

    float nextFireTime;
    bool reloading;
    Coroutine reloadRoutine;


    public WeaponDefinition Definition => def;

    public void InitAmmo()
    {
        ammoInMag = def.magazineSize;
        reserveAmmo = def.maxReserve;
    }

    public void SetAmmo(int mag, int reserve)
    {
        ammoInMag = mag;
        reserveAmmo = reserve;
    }

    public (int mag, int reserve) GetAmmo() => (ammoInMag, reserveAmmo);

    public bool IsReloading => reloading;

    public bool TryReload(MonoBehaviour runner)
    {
        if (reloading) return false;
        if (ammoInMag >= def.magazineSize) return false;
        if (reserveAmmo <= 0) return false;

        reloadRoutine = runner.StartCoroutine(ReloadRoutine());
        return true;
    }

    IEnumerator ReloadRoutine()
    {
        reloading = true;
        yield return new WaitForSeconds(def.reloadTime);

        int need = def.magazineSize - ammoInMag;
        int take = Mathf.Min(need, reserveAmmo);
        ammoInMag += take;
        reserveAmmo -= take;

        reloading = false;
        reloadRoutine = null;
    }

    public void CancelReload()
    {
        if (!reloading) return;

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloading = false;
        reloadRoutine = null;
    }


    public bool TryFire(Camera cam, GameObject owner)
    {
        if (reloading) return false;
        if (Time.time < nextFireTime) return false;
        if (ammoInMag <= 0) return false;

        nextFireTime = Time.time + (1f / def.fireRate);
        ammoInMag--;

        FireRay(cam, owner);
        PlayFX();

        return true;
    }

    void FireRay(Camera cam, GameObject owner)
    {
        Vector3 dir = ApplySpread(cam.transform.forward, def.spreadDegrees);

        Vector3 origin = cam.transform.position;
        Ray ray = new Ray(origin, dir);

        Vector3 tracerStart = (muzzle != null) ? muzzle.position : origin;

        if (Physics.Raycast(ray, out RaycastHit hit, def.range, hitMask, QueryTriggerInteraction.Ignore))
        {
            SpawnTracer(tracerStart, hit.point);

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
                dmg.TakeDamage(def.damage, hit.point, hit.normal, owner);

            if (def.impactPrefab != null)
                Instantiate(def.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }
        else
        {
            SpawnTracer(tracerStart, origin + dir * def.range);
        }
    }



    Vector3 ApplySpread(Vector3 dir, float degrees)
    {
        if (degrees <= 0f) return dir;

        float rad = degrees * Mathf.Deg2Rad;
        Vector2 r = Random.insideUnitCircle * Mathf.Tan(rad);
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 up = Vector3.Cross(dir, right).normalized;

        return (dir + right * r.x + up * r.y).normalized;
    }

    void PlayFX()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play(true);

        if (def.muzzleFlashPrefab != null && muzzle != null)
            Instantiate(def.muzzleFlashPrefab, muzzle.position, muzzle.rotation, muzzle);

        if (audioSource != null && def.shotSound != null)
            audioSource.PlayOneShot(def.shotSound);
    }

    void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (tracerPrefab == null) return;

        TrailRenderer t = Instantiate(tracerPrefab, start, Quaternion.identity);
        StartCoroutine(AnimateTracer(t, start, end));
    }
    IEnumerator AnimateTracer(TrailRenderer t, Vector3 start, Vector3 end)
    {
        float dist = Vector3.Distance(start, end);
        float time = dist / Mathf.Max(1f, tracerSpeed);

        float elapsed = 0f;
        while (elapsed < time)
        {
            if (t == null) yield break;
            t.transform.position = Vector3.Lerp(start, end, elapsed / time);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (t != null) t.transform.position = end;

        // let trail fade out, then destroy
        Destroy(t.gameObject, t.time);
    }
}
