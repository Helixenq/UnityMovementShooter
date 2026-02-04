using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform targetRoot;     // Player root (has collider + vitals)
    [SerializeField] Transform targetAim;      // AimPoint (chest). If null we fallback to collider center.

    [Header("Ranges")]
    [SerializeField] float detectRange = 25f;
    [SerializeField] float attackRange = 15f;

    [Header("Weapon")]
    [SerializeField] float fireCooldown = 0.35f;
    [SerializeField] float damage = 8f;

    [Header("Line of sight")]
    [SerializeField] LayerMask losMask = ~0;   // set in inspector (exclude Enemy layer, etc.)
    [SerializeField] float muzzleHeight = 1.5f;

    NavMeshAgent agent;
    float nextShot;
    Collider targetCol;
    DamageableVitals targetVitals;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (targetRoot != null)
        {
            targetCol = targetRoot.GetComponentInChildren<Collider>();
            targetVitals = targetRoot.GetComponentInChildren<DamageableVitals>();
        }
    }

    void Update()
    {
        if (targetRoot == null) return;
        if (targetVitals != null && targetVitals.IsDead) { agent.isStopped = true; return; }

        float d = Vector3.Distance(transform.position, targetRoot.position);
        if (d > detectRange)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(targetRoot.position);

        if (d <= attackRange && Time.time >= nextShot)
        {
            nextShot = Time.time + fireCooldown;
            TryShoot();
        }
    }

    void TryShoot()
    {
        Vector3 origin = transform.position + Vector3.up * muzzleHeight;

        Vector3 aimPoint = GetAimPoint();
        Vector3 dir = (aimPoint - origin).normalized;

        // IMPORTANT: ignore self hits + use LOS mask
        if (Physics.Raycast(origin, dir, out RaycastHit hit, attackRange, losMask, QueryTriggerInteraction.Ignore))
        {
            // If we accidentally hit ourselves (rare but possible), ignore.
            if (hit.collider.transform.IsChildOf(transform)) return;

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
                dmg.TakeDamage(damage, hit.point, hit.normal, gameObject);
        }

        Debug.DrawRay(origin, dir * attackRange, Color.red, 0.1f);
    }

    Vector3 GetAimPoint()
    {
        if (targetAim != null) return targetAim.position;
        if (targetCol != null) return targetCol.bounds.center;
        return targetRoot.position + Vector3.up * 1.2f;
    }
}
