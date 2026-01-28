using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 30;
    [SerializeField] private int _defense = 20;

    [Header("Death Settings")]
    [SerializeField] private float _deathAnimationDuration = 2f;
    [SerializeField] private float _corpseDuration = 5f;
    [SerializeField] private bool _destroyOnDeath = true;
    [SerializeField] private GameObject _deathEffectPrefab;
    [SerializeField] private AudioClip _deathSound;

    [Header("Animation Parameters")]
    [SerializeField] private string _deathTriggerName = "Death";
    [SerializeField] private string _deathLayerName = "Death";
    [SerializeField] private string _attackBoolName = "attack";

    [Header("Visual Effects")]
    [SerializeField] private GameObject _deathEffect;
    [SerializeField] private Color _hitFlashColor = Color.red;
    [SerializeField] private float _hitFlashDuration = 0.4f;

    // Компоненты
    private int _currentHealth;
    private bool _isDead = false;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Animator _animator;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private EnemyController8Directions _enemyController;

    // Анимационные параметры
    private int _deathLayerIndex = -1;
    private float _deathTime = 0f;

    // Состояния
    private Vector2 _lastHitDirection = Vector2.down;

    void Start()
    {
        _currentHealth = _maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _enemyController = GetComponent<EnemyController8Directions>();

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        // Инициализация аниматора смерти
        if (_animator != null)
        {
            _deathLayerIndex = _animator.GetLayerIndex(_deathLayerName);
            if (_deathLayerIndex == -1)
            {
                Debug.LogWarning($"Death layer '{_deathLayerName}' not found. Using Base Layer.");
            }
            else
            {
                // По умолчанию слой смерти отключен
                _animator.SetLayerWeight(_deathLayerIndex, 0f);
            }
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

        // Сохраняем направление удара для анимации смерти
        _lastHitDirection = attackDirection;

        _currentHealth -= damage;

        // Визуальный эффект попадания
        FlashOnHit();

        Debug.Log($"❤️ {gameObject.name} health: {_currentHealth}/{_maxHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void FlashOnHit()
    {
        if (_spriteRenderer != null)
        {
            StartCoroutine(FlashHitCoroutine());
        }
    }

    private IEnumerator FlashHitCoroutine()
    {
        if (_spriteRenderer == null) yield break;

        _spriteRenderer.color = _hitFlashColor;
        yield return new WaitForSeconds(_hitFlashDuration);
        _spriteRenderer.color = _originalColor;
    }

    public int GetDefense()
    {
        return _defense;
    }

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        _deathTime = Time.time;

        Debug.Log($"💀 ENEMY DIED: {gameObject.name}");

        // Получаем направление для анимации смерти
        Vector2 deathDirection = GetDeathDirection();
        Debug.Log($"Death direction: {deathDirection}");

        // Останавливаем все корутины
        StopAllCoroutines();

        // Отключаем компоненты контроллера
        if (_enemyController != null)
        {
            _enemyController.OnDeath();
        }

        // Запускаем анимацию смерти
        StartDeathAnimation(deathDirection);

        // Отключаем физику и коллайдеры
        DisablePhysics();

        // Звук смерти
        if (_deathSound != null)
        {
            AudioSource.PlayClipAtPoint(_deathSound, transform.position);
        }

        // Эффект смерти
        if (_deathEffect != null)
        {
            Instantiate(_deathEffect, transform.position, Quaternion.identity);
        }

        // Альтернативный эффект смерти
        if (_deathEffectPrefab != null)
        {
            Instantiate(_deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // Запускаем корутину уничтожения
        StartCoroutine(DeathSequence());
    }

    private Vector2 GetDeathDirection()
    {
        // Пытаемся получить направление от контроллера
        if (_enemyController != null)
        {
            // Пробуем получить направление атаки игрока
            Vector2 controllerDirection = _enemyController.GetLastAttackDirection();
            if (controllerDirection != Vector2.zero)
            {
                return controllerDirection;
            }

            // Или направление к игроку
            controllerDirection = _enemyController.GetDirectionToPlayer();
            if (controllerDirection != Vector2.zero)
            {
                return controllerDirection;
            }
        }

        // Если не получилось, используем последнее направление удара
        if (_lastHitDirection != Vector2.zero)
        {
            return _lastHitDirection;
        }

        // Если все равно нет, используем направление вниз по умолчанию
        Debug.LogWarning("No death direction found, using default (down)");
        return Vector2.down;
    }

    private void StartDeathAnimation(Vector2 deathDirection)
    {
        if (_animator == null || !_animator.enabled)
        {
            Debug.LogWarning("Animator is null or disabled, cannot play death animation");
            return;
        }

        Debug.Log("=== STARTING DEATH ANIMATION ===");
        Debug.Log($"Death direction: {deathDirection}");

        // Полный сброс аниматора
        _animator.Rebind();
        _animator.Update(0f);

        // Отключаем все активные анимации
        _animator.SetBool(_attackBoolName, false);
        _animator.SetFloat("Speed", 0f);
        _animator.SetFloat("Horizontal", 0f);
        _animator.SetFloat("Vertical", 0f);

        // Устанавливаем направление смерти для Blend Tree
        Vector2 normalizedDir = deathDirection.normalized;
        _animator.SetFloat("DeathX", normalizedDir.x);
        _animator.SetFloat("DeathY", normalizedDir.y);

        Debug.Log($"Death direction set: X={normalizedDir.x}, Y={normalizedDir.y}");

        // Включаем слой смерти если он существует
        if (_deathLayerIndex != -1)
        {
            _animator.SetLayerWeight(_deathLayerIndex, 1f);
            Debug.Log($"Death layer enabled with weight: {_animator.GetLayerWeight(_deathLayerIndex)}");
        }

        // Устанавливаем триггер смерти
        _animator.SetTrigger(_deathTriggerName);

        // Принудительно запускаем анимацию смерти
        if (_deathLayerIndex != -1)
        {
            _animator.Play("Death", _deathLayerIndex, 0f);
        }
        else
        {
            _animator.Play("Death", 0, 0f);
        }

        _animator.Update(0f);

        Debug.Log("Death animation triggered");
    }

    private void DisablePhysics()
    {
        // Отключаем Rigidbody
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            _rb.isKinematic = true;
            _rb.simulated = false;
        }

        // Отключаем коллайдер
        if (_collider != null)
        {
            _collider.enabled = false;
        }

        // Отключаем другие коллайдеры
        Collider2D[] allColliders = GetComponents<Collider2D>();
        foreach (Collider2D coll in allColliders)
        {
            coll.enabled = false;
        }

        Debug.Log($"🔌 Disabled physics on {gameObject.name}");
    }

    private IEnumerator DeathSequence()
    {
        Debug.Log("Death sequence started...");

        // Проверяем, запустилась ли анимация смерти
        if (_animator != null && _animator.enabled)
        {
            yield return new WaitForSeconds(0.1f);

            // Проверяем состояние анимации
            AnimatorStateInfo stateInfo;
            if (_deathLayerIndex != -1)
            {
                stateInfo = _animator.GetCurrentAnimatorStateInfo(_deathLayerIndex);
            }
            else
            {
                stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            }

            Debug.Log($"Death animation state: {stateInfo.fullPathHash}");
            Debug.Log($"Is playing death: {stateInfo.IsName("Death")}");

            // Если анимация не запустилась, пытаемся еще раз
            if (!stateInfo.IsName("Death") || stateInfo.normalizedTime < 0.1f)
            {
                Debug.LogWarning("Death animation didn't start properly, forcing...");
                Vector2 deathDirection = GetDeathDirection();
                StartDeathAnimation(deathDirection);
            }
        }

        // Ждем окончания анимации смерти
        yield return new WaitForSeconds(_deathAnimationDuration);

        Debug.Log("Death animation finished");

        // Делаем врага полупрозрачным
        if (_spriteRenderer != null)
        {
            Color corpseColor = _spriteRenderer.color;
            corpseColor.a = 0.5f;
            _spriteRenderer.color = corpseColor;
        }

        // Ждем пока труп будет виден
        yield return new WaitForSeconds(_corpseDuration);

        Debug.Log("Destroying enemy...");

        // Уничтожаем объект или отключаем его
        if (_destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Если враг мертв, проверяем анимацию
        if (_isDead && _animator != null && _animator.enabled)
        {
            // Если прошло меньше 0.5 секунд после смерти и анимация не запустилась
            if (Time.time - _deathTime < 0.5f)
            {
                // Проверяем состояние анимации
                AnimatorStateInfo stateInfo;
                if (_deathLayerIndex != -1)
                {
                    stateInfo = _animator.GetCurrentAnimatorStateInfo(_deathLayerIndex);
                }
                else
                {
                    stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                }

                // Если анимация смерти не играет, запускаем ее принудительно
                if (!stateInfo.IsName("Death") && stateInfo.normalizedTime < 0.1f)
                {
                    Debug.Log("FORCING DEATH ANIMATION IN UPDATE!");
                    Vector2 deathDirection = GetDeathDirection();
                    StartDeathAnimation(deathDirection);
                }
            }
        }
    }

    // Метод для тестирования смерти
    [ContextMenu("Test Death Animation")]
    public void TestDeathAnimation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("This test only works in Play Mode");
            return;
        }

        Debug.Log("=== TESTING DEATH ANIMATION ===");
        Vector2 testDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        TakeDamage(_currentHealth + 1, testDirection);
    }

    // Для отладки в редакторе
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying && !_isDead)
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

    // Геттеры для доступа из других скриптов
    public bool IsDead() => _isDead;
    public float GetHealthPercent() => (float)_currentHealth / _maxHealth;
    public int GetCurrentHealth() => _currentHealth;
    public int GetMaxHealth() => _maxHealth;
}