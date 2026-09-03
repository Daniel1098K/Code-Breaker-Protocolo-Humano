using UnityEngine;
using System.Collections;

/// <summary>
/// LaserController.cs — Code-Breaker: Protocolo Humano
/// Adjuntar como hijo del MuzzlePoint en la jerarquía del arma.
/// NO usar como prefab instanciado — asignar directo en escena.
/// PlayerShooting llama a Fire() en cada disparo de la Ping.
/// </summary>
public class LaserController : MonoBehaviour
{
    // ─── Componentes ───────────────────────────────────────────────────────────
    [Header("Componentes")]
    [Tooltip("LineRenderer que dibuja el haz — debe tener Use World Space = true")]
    public LineRenderer beamLine;

    public ParticleSystem psBeamGlow;
    public ParticleSystem psImpactFlash;
    public ParticleSystem psSparks;
    public ParticleSystem psSmoke;

    // ─── Configuración ─────────────────────────────────────────────────────────
    [Header("Configuración")]
    public float maxRange     = 50f;
    public float beamDuration = 0.12f;
    public LayerMask hitLayers;

    // ─── Estado ────────────────────────────────────────────────────────────────
    private Coroutine _fireCoroutine;

    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Asegurarse de que el beam esté apagado al inicio
        if (beamLine != null) beamLine.enabled = false;
    }

    /// <summary>
    /// Dispara el láser. Llamar desde PlayerShooting.FirePing().
    /// origin:    posición del muzzlePoint en world space
    /// direction: dirección normalizada hacia el punto de impacto
    /// </summary>
    public void Fire(Vector3 origin, Vector3 direction)
    {
        // Si ya hay un disparo activo, interrumpirlo y disparar de nuevo
        // (permite cadencia rápida sin bloqueos)
        if (_fireCoroutine != null)
        {
            StopCoroutine(_fireCoroutine);
            CleanUp();
        }

        _fireCoroutine = StartCoroutine(FireRoutine(origin, direction));
    }

    private IEnumerator FireRoutine(Vector3 origin, Vector3 dir)
    {
        // ── 1. Raycast para punto de impacto ───────────────────────────────────
        Vector3 endpoint;
        bool didHit = Physics.Raycast(origin, dir, out RaycastHit hit, maxRange, hitLayers,
                                      QueryTriggerInteraction.Ignore);
        if (didHit)
        {
            endpoint = hit.point;
            SpawnImpactEffects(hit.point, hit.normal);
        }
        else
        {
            endpoint = origin + dir * maxRange;
        }

        // ── 2. Dibujar el LineRenderer en world space ──────────────────────────
        if (beamLine != null)
        {
            beamLine.enabled = true;
            beamLine.SetPosition(0, origin);    // inicio: boca del arma
            beamLine.SetPosition(1, endpoint);  // fin:    punto de impacto
        }

        // ── 3. Partículas del haz ──────────────────────────────────────────────
        if (psBeamGlow != null) psBeamGlow.Play();

        // ── 4. Esperar duración del disparo ────────────────────────────────────
        yield return new WaitForSeconds(beamDuration);

        CleanUp();
        _fireCoroutine = null;
    }

    private void SpawnImpactEffects(Vector3 pos, Vector3 normal)
    {
        Quaternion rot = Quaternion.LookRotation(normal);

        if (psImpactFlash != null)
        {
            psImpactFlash.transform.position = pos;
            psImpactFlash.transform.rotation = rot;
            psImpactFlash.Play();
        }
        if (psSparks != null)
        {
            psSparks.transform.position = pos;
            psSparks.transform.rotation = rot;
            psSparks.Play();
        }
        if (psSmoke != null)
        {
            psSmoke.transform.position = pos;
            psSmoke.Play();
        }
    }

    private void CleanUp()
    {
        if (beamLine  != null) beamLine.enabled = false;
        if (psBeamGlow != null) psBeamGlow.Stop();
    }
}