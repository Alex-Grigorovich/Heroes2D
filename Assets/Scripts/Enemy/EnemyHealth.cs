using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 30;
    [SerializeField] private int _defense = 20;

    [Header("Visual Effects")]
    [SerializeField] private GameObject _deathEffect;

    private int _currentHealth;
    private bool _isDead = false;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;

    void Start()
    {
        _currentHealth = _maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
        Debug.Log($"🔄 Enemy spawned: {gameObject.name}, Health: {_currentHealth}/{_maxHealth}");
    }

    public void TakeDamage(int damage, Vector2 attackDirection)
    {
        if (_isDead)
        {
            Debug.Log($"💀 Enemy {gameObject.name} is already dead!");
            return;
        }

        Debug.Log($"🎯 {gameObject.name} taking {damage} damage! Current health: {_currentHealth}");

        _currentHealth -= damage;

        // Визуальный эффект попадания
        if (_spriteRenderer != null)
        {
            StartCoroutine(FlashRed());
        }

        Debug.Log($"❤️ {gameObject.name} health: {_currentHealth}/{_maxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRed()
    {
        if (_spriteRenderer == null) yield break;

        // Мигаем красным в течение 1 секунды
        float flashDuration = 1f;
        float flashInterval = 0.1f;
        float elapsed = 0f;

        while (elapsed < flashDuration && !_isDead)
        {
            // Красный цвет
            _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(flashInterval);

            // Оригинальный цвет
            _spriteRenderer.color = _originalColor;
            yield return new WaitForSeconds(flashInterval);

            elapsed += flashInterval * 2f;
        }

        // Гарантируем, что цвет вернется к оригинальному
        if (_spriteRenderer != null && !_isDead)
        {
            _spriteRenderer.color = _originalColor;
        }
    }

    public int GetDefense()
    {
        return 50; // Пример значения
    }

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        Debug.Log($"💀 ENEMY DIED: {gameObject.name}");

        // Останавливаем все корутины
        StopAllCoroutines();

        // Эффект смерти
        if (_deathEffect != null)
        {
            Instantiate(_deathEffect, transform.position, Quaternion.identity);
        }

        // Отключаем компоненты
        DisableEnemyComponents();

        // Уничтожаем объект
        Destroy(gameObject, 2f);
    }

    private void DisableEnemyComponents()
    {
        // Отключаем коллайдер
        if (TryGetComponent<Collider2D>(out Collider2D collider))
        {
            collider.enabled = false;
        }

        // Отключаем Rigidbody
        if (TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
        {
            rb.simulated = false;
        }

        // Отключаем скрипт врага
        if (TryGetComponent<EnemyController8Directions>(out EnemyController8Directions controller))
        {
            controller.enabled = false;
        }

        // Отключаем аниматор
        if (TryGetComponent<Animator>(out Animator animator))
        {
            animator.enabled = false;
        }

        Debug.Log($"🔌 Disabled components on {gameObject.name}");
    }

    // Для отладки в редакторе
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            // Полоска здоровья над врагом
            float healthPercent = (float)_currentHealth / _maxHealth;
            Vector3 healthBarStart = transform.position + Vector3.up * 0.8f;
            Vector3 healthBarEnd = healthBarStart + Vector3.right * healthPercent;

            Gizmos.color = healthPercent > 0.5f ? Color.green :
                          healthPercent > 0.25f ? Color.yellow : Color.red;
            Gizmos.DrawLine(healthBarStart, healthBarEnd);

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(healthBarStart + Vector3.right * 0.5f, new Vector3(1f, 0.1f, 0f));
        }
    }
}