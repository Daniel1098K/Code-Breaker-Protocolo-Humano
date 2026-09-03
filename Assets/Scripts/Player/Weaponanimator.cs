using System.Collections;
using UnityEngine;

/// <summary>
/// WeaponAnimator.cs - Code-Breaker: Protocolo Humano
/// Controla las animaciones del arma equipada.
/// Adjuntar al mismo GameObject que tiene el Animator (FpsGlock / ArmModel).
/// PlayerShooting llama a los métodos públicos en los momentos correctos.
/// </summary>
[RequireComponent(typeof(Animator))]
public class WeaponAnimator : MonoBehaviour
{
    private Animator animator;

    // Hashes precomputados (más eficiente que strings en Update)
    private static readonly int HashShooting   = Animator.StringToHash("isShooting");
    private static readonly int HashReloading  = Animator.StringToHash("isReloading");
    private static readonly int HashInspecting = Animator.StringToHash("isInspecting");

    [Tooltip("Duración del clip Shoot en segundos (para resetear el bool automáticamente)")]
    public float shootClipDuration = 0.2f;

    [Tooltip("Duración del clip Reload en segundos")]
    public float reloadClipDuration = 2f;

    [Tooltip("Duración del clip Inspect en segundos")]
    public float inspectClipDuration = 3f;

    private Coroutine shootResetCoroutine;

    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // ─── API Pública (llamada desde PlayerShooting) ────────────────────────────

    /// <summary>
    /// Disparar: activa Shoot y vuelve a Idle automáticamente.
    /// </summary>
    public void PlayShoot()
    {
        // Reiniciar corrutina si dispara antes de que termine el clip anterior
        if (shootResetCoroutine != null)
            StopCoroutine(shootResetCoroutine);

        animator.SetBool(HashShooting, true);
        shootResetCoroutine = StartCoroutine(ResetShootAfterClip());
    }

    /// <summary>
    /// Recargar: bloquea disparo durante la animación.
    /// PlayerShooting debe esperar a que kernelIsReloading = false.
    /// </summary>
    public void PlayReload()
    {
        animator.SetBool(HashReloading, true);
        StartCoroutine(ResetReloadAfterClip());
    }

    /// <summary>
    /// Inspect: se activa al mantener Tab o tecla configurable.
    /// Se cancela si el jugador dispara o recarga.
    /// </summary>
    public void PlayInspect()
    {
        if (animator.GetBool(HashReloading) || animator.GetBool(HashShooting)) return;
        animator.SetBool(HashInspecting, true);
        StartCoroutine(ResetInspectAfterClip());
    }

    /// <summary>
    /// Cancelar cualquier animación y volver a Idle inmediatamente.
    /// Útil al cambiar de arma.
    /// </summary>
    public void ForceIdle()
    {
        StopAllCoroutines();
        animator.SetBool(HashShooting,   false);
        animator.SetBool(HashReloading,  false);
        animator.SetBool(HashInspecting, false);
    }

    // ─── Corrutinas de reset ───────────────────────────────────────────────────

    IEnumerator ResetShootAfterClip()
    {
        yield return new WaitForSeconds(shootClipDuration);
        animator.SetBool(HashShooting, false);
    }

    IEnumerator ResetReloadAfterClip()
    {
        yield return new WaitForSeconds(reloadClipDuration);
        animator.SetBool(HashReloading, false);
    }

    IEnumerator ResetInspectAfterClip()
    {
        yield return new WaitForSeconds(inspectClipDuration);
        animator.SetBool(HashInspecting, false);
    }
}