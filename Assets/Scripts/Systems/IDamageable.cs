/// <summary>
/// IDamageable.cs - Code-Breaker: Protocolo Humano
/// Interfaz implementada por cualquier objeto que pueda recibir daño.
/// Permite a PlayerShooting aplicar daño sin depender de clases concretas.
/// Implementan esta interfaz: BaseEnemy (y subclases), PlayerHealth, objetos destructibles.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}