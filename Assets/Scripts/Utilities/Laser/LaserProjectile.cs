using UnityEngine;

/// <summary>
/// LaserProjectile.cs — Code-Breaker: Protocolo Humano
///
/// Proyectil láser visible que viaja desde el muzzle hasta el punto de impacto.
/// NO usa Rigidbody. Se mueve con Transform.Translate cada frame.
///
/// Estructura del prefab (LaserBullet):
///   LaserBullet             ← GameObject vacío, LaserProjectile.cs aquí
///   ├── BulletCore          ← ParticleSystem principal (esfera azul), asignar a bulletCore
///   └── BulletTrail         ← ParticleSystem hijo (cola), asignar a bulletTrail
///
/// Los VFX de impacto son prefabs SEPARADOS asignados en el Inspector:
///   - psBeamGlowPrefab      (PS_BeamGlow)
///   - psImpactFlashPrefab   (PS_ImpactFlash)
///   - psSparksPrefab        (PS_Sparks)
///   - psSmokePrefab         (PS_Smoke)
/// </summary>
public class LaserProjectile : MonoBehaviour
{
    // ─── VFX del proyectil ─────────────────────────────────────────────────────
    [Header("Particle Systems (hijos del prefab)")]
    [Tooltip("BulletCore — ParticleSystem de la esfera principal")]
    public ParticleSystem bulletCore;

    [Tooltip("BulletTrail — ParticleSystem de la cola trasera")]
    public ParticleSystem bulletTrail;

    [Header("Prefabs de impacto (desde Assets)")]
    public GameObject psBeamGlowPrefab;
    public GameObject psImpactFlashPrefab;
    public GameObject psSparksPrefab;
    public GameObject psSmokePrefab;

    // ─── Configuración ─────────────────────────────────────────────────────────
    [Header("Configuración")]
    [Tooltip("Velocidad del proyectil en m/s")]
    public float speed = 80f;

    [Tooltip("Vida máxima si no impacta nada")]
    public float maxLifetime = 2f;

    [Tooltip("Layers que puede impactar")]
    public LayerMask hitLayers;

    // ─── Privados ──────────────────────────────────────────────────────────────
    private Vector3 direction;
    private float   damage;
    private float   distanceTraveled = 0f;
    private float   maxDistance;
    private bool    hasHit = false;

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Llamar desde PlayerShooting justo después de Instantiate().
    /// targetPoint: punto de impacto calculado por raycast en PlayerShooting.
    /// </summary>
    public void Launch(Vector3 origin, Vector3 targetPoint, float dmg)
    {
        damage      = dmg;
        direction   = (targetPoint - origin).normalized;
        maxDistance = Vector3.Distance(origin, targetPoint);

        // Posicionar y orientar el proyectil hacia el destino
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(direction);

        // Arrancar ambos particle systems
        if (bulletCore  != null) bulletCore.Play();
        if (bulletTrail != null) bulletTrail.Play();

        Destroy(gameObject, maxLifetime);
    }

    void Update()
    {
        if (hasHit) return;

        // ── Mover el proyectil ─────────────────────────────────────────────────
        float step = speed * Time.deltaTime;
        transform.Translate(Vector3.forward * step, Space.Self);
        distanceTraveled += step;

        // ── Comprobar si llegó al punto de impacto ─────────────────────────────
        if (distanceTraveled >= maxDistance)
        {
            OnReachTarget();
            return;
        }

        // ── Raycast adelante para detectar colisiones en el camino ─────────────
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit,
            step * 2f, hitLayers, QueryTriggerInteraction.Ignore))
        {
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage);

            OnImpact(hit.point, hit.normal);
        }
    }

    // ─── Llegó al destino (sin impacto en collider) ────────────────────────────
    void OnReachTarget()
    {
        hasHit = true;
        SpawnImpactVFX(transform.position, -direction);
        DestroyProjectile();
    }

    // ─── Impactó en un collider ────────────────────────────────────────────────
    void OnImpact(Vector3 point, Vector3 normal)
    {
        hasHit = true;
        SpawnImpactVFX(point, normal);
        DestroyProjectile();
    }

    // ─── Spawnear VFX individuales desde Assets ────────────────────────────────
    void SpawnImpactVFX(Vector3 pos, Vector3 normal)
    {
        Quaternion rot = normal != Vector3.zero
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        SpawnAndAutoDestroy(psImpactFlashPrefab, pos, rot);
        SpawnAndAutoDestroy(psSparksPrefab,      pos, rot);
        SpawnAndAutoDestroy(psSmokePrefab,       pos, Quaternion.identity);
        SpawnAndAutoDestroy(psBeamGlowPrefab,    pos, rot);
    }

    void SpawnAndAutoDestroy(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;

        GameObject vfx = Instantiate(prefab, pos, rot);

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(vfx, duration + 0.5f);
        }
        else
        {
            Destroy(vfx, 2f);
        }
    }

    // ─── Detener partículas y destruir el proyectil ────────────────────────────
    void DestroyProjectile()
    {
        // Detener emisión pero dejar que las partículas existentes terminen su vida
        if (bulletCore  != null) bulletCore.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (bulletTrail != null) bulletTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(gameObject);
    }
}