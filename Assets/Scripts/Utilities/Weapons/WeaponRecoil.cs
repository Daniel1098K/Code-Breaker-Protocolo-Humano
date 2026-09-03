using System.Collections;
using UnityEngine;

/// <summary>
/// WeaponRecoil.cs - Code-Breaker: Protocolo Humano
/// Retroceso procedural del arma al disparar. No requiere Animation Clips.
/// Adjuntar al GameObject del arma (FpsGlock / hijo de la cámara).
/// Llamar TriggerRecoil() desde PlayerShooting al disparar.
/// </summary>
public class WeaponRecoil : MonoBehaviour
{
    [Header("Recoil Settings")]
    [Tooltip("Cuánto retrocede hacia atrás en el eje Z (metros)")]
    public float recoilDistance = 0.05f;

    [Tooltip("Cuánto rota hacia arriba (grados)")]
    public float recoilRotation = 5f;

    [Tooltip("Velocidad del retroceso (ida)")]
    public float recoilSpeed = 20f;

    [Tooltip("Velocidad de retorno a posición original")]
    public float returnSpeed = 8f;

    // Estado
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private bool isRecoiling = false;

    void Start()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    void Update()
    {
        if (!isRecoiling)
        {
            // Retorno suave a posición original
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, originalLocalPosition, Time.deltaTime * returnSpeed);

            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, originalLocalRotation, Time.deltaTime * returnSpeed);
        }
    }

    /// <summary>
    /// Llamar desde PlayerShooting.FirePing() y FireKernel() al disparar.
    /// </summary>
    public void TriggerRecoil()
    {
        StopAllCoroutines();
        StartCoroutine(RecoilCoroutine());
    }

    IEnumerator RecoilCoroutine()
    {
        isRecoiling = true;

        // Posición y rotación objetivo del retroceso
        Vector3 recoilPos = originalLocalPosition + Vector3.back * recoilDistance;
        Quaternion recoilRot = originalLocalRotation * Quaternion.Euler(-recoilRotation, 0f, 0f);

        // Fase 1: ir hacia posición de retroceso
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * recoilSpeed;
            transform.localPosition = Vector3.Lerp(originalLocalPosition, recoilPos, t);
            transform.localRotation = Quaternion.Slerp(originalLocalRotation, recoilRot, t);
            yield return null;
        }

        isRecoiling = false;
        // El Update() se encarga del retorno suave
    }
}