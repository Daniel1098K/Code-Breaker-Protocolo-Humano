using UnityEngine;

/// <summary>
/// PingProjectile.cs - Code-Breaker: Protocolo Humano
/// Adjuntar al prefab de proyectil visual de la Pistola Ping.
/// El prefab debe tener: SphereCollider (trigger), Rigidbody, TrailRenderer, este script.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class PingProjectile : MonoBehaviour
{
    [Tooltip("Prefab de VFX que se instancia al impactar (chispas azules)")]
    public GameObject impactVFXPrefab;

    [Tooltip("Tiempo en segundos antes de auto-destruirse si no impacta")]
    public float lifetime = 3f;

    void Start()
    {
        // Asegurarse de que el collider sea trigger (no empuja físicamente)
        GetComponent<SphereCollider>().isTrigger = true;

        // Desactivar gravedad — el proyectil viaja recto
        GetComponent<Rigidbody>().useGravity = false;

        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Ignorar colisión con el jugador
        if (other.CompareTag("Player")) return;

        // Spawn VFX de impacto
        if (impactVFXPrefab != null)
            Instantiate(impactVFXPrefab, transform.position, Quaternion.LookRotation(-transform.forward));

        // El daño ya fue aplicado por hitscan en PlayerShooting.FirePing()
        // Este proyectil es solo visual — se destruye al tocar algo
        Destroy(gameObject);
    }
}