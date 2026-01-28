using UnityEngine;
using System.Collections;

public class EnemyController8DirectionsWithDeath : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRange = 5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private LayerMask _obstacleLayer = 1;

    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _stoppingDistance = 1.2f;

    [Header("Combat Settings")]
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _attackDamage = 25f;
    [SerializeField] private float _staminaDamageToShield = 15f;

    [Header("Attack Timing")]
    [SerializeField] private float _windupDuration = 0.3f;
    [SerializeField] private float _attackDelay = 0.6f;
    [SerializeField] private float _attackDuration = 0.2f;
    [SerializeField] private float _recoveryDuration = 0.5f;

    [Header("Health & Death Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _deathAnimationDuration = 2f;
    [SerializeField] private float _corpseDuration = 5f;
    [SerializeField] private bool _destroyOnDeath = true;
    [SerializeField] private GameObject _deathEffectPrefab;

    [Header("Enemy Zone Settings")]
    [SerializeField] private GameObject _enemyZonePrefab;
    [SerializeField] private float _enemyZoneDistance = 0.7f;
    [SerializeField] private float _enemyZoneShowDuration = 0.8f;
    [SerializeField] private float _enemyZoneRadius = 0.5f;

    [Header("Block Settings")]
    [SerializeField] private bool _canBeBlocked = true;
    [SerializeField] private GameObject _blockEffectPrefab;
    [SerializeField] private AudioClip _blockSound;
    [SerializeField] private float _blockPushbackForce = 1f;

    [Header("Visual Effects")]
    [SerializeField] private Color _playerHitColor = Color.red;
    [SerializeField] private Color _blockedHitColor = Color.blue;
    [SerializeField] private float _hitFlashDuration = 0.4f;
    [SerializeField] private float _blockFlashDuration = 0.3f;

    [Header("Animation Parameters")]
    [SerializeField] private string _deathTriggerName = "Death";
    [SerializeField] private string _attackBoolName = "attack";

    // Ссылки на компоненты
    private Transform _player;
    private Rigidbody2D _rb;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _playerSprite;
    private Color _playerOriginalColor;
    private Color _originalColor;

    // Системы игрока
    private PlayerShieldSystem _playerShieldSystem;
    private HealthSystem _playerHealthSystem;

    // Состояние врага
    private float _currentHealth;
    private bool _isDead = false;
    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private bool _shouldStopMoving = false;
    private float _lastAttackTime = 0f;
    private Vector2 _attackDirection = Vector2.down;
    private Vector2 _deathDirection = Vector2.down; // Направление смерти для Blend Tree

    // Зона атаки
    private GameObject _enemyZoneInstance;
    private SpriteRenderer _enemyZoneRenderer;
    private bool _isDealingDamage = false;

    // Визуальные эффекты
    private Coroutine _flashCoroutine;
    private bool _isFlashing = false;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        _currentHealth = _maxHealth;

        FindAndSetupPlayer();
    }

    void FindAndSetupPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
            _playerSprite = _player.GetComponent<SpriteRenderer>();
            if (_playerSprite != null)
            {
                _playerOriginalColor = _playerSprite.color;
            }

            _playerShieldSystem = playerObject.GetComponent<PlayerShieldSystem>();
            _playerHealthSystem = HealthSystem.Instance;

            Debug.Log($"Player found: {playerObject.name}");
        }
        else
        {
            Debug.LogError("Player GameObject not found!");
        }

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.drag = 10f;
            _rb.angularDrag = 10f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void Update()
    {
        // Если враг мертв, не обновляем логику
        if (_isDead)
        {
            return;
        }

        if (_player == null)
        {
            FindAndSetupPlayer();
            if (_player == null) return;
        }

        CheckForPlayer();

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        if (_isPlayerDetected)
        {
            bool shouldBeInAttackPosition = distanceToPlayer <= _attackRange;
            bool reachedStoppingDistance = distanceToPlayer <= _stoppingDistance;

            _shouldStopMoving = shouldBeInAttackPosition || reachedStoppingDistance;

            if (shouldBeInAttackPosition && CanAttack() && !_isAttacking)
            {
                StopMovingImmediately();
                StartAttack();
            }
            else if (!_shouldStopMoving && !_isAttacking)
            {
                MoveTowardsPlayer();
            }
            else if (!_isAttacking)
            {
                StopMovingImmediately();

                if (shouldBeInAttackPosition && CanAttack())
                {
                    StartAttack();
                }
            }
        }
        else if (!_isAttacking)
        {
            StopMovingImmediately();
        }

        if (_isDealingDamage && _enemyZoneInstance != null)
        {
            CheckEnemyZoneDamage();
        }

        UpdateAnimation();
    }

    public void TakeDamage(float damage, Vector2 hitDirection)
    {
        if (_isDead) return;

        _currentHealth -= damage;
        Debug.Log($"Enemy took {damage} damage. Health: {_currentHealth}/{_maxHealth}");

        // Визуальный эффект получения урона
        FlashEnemyOnHit();

        if (_currentHealth <= 0)
        {
            Die(hitDirection);
        }
    }

    private void FlashEnemyOnHit()
    {
        if (_spriteRenderer != null && !_isFlashing)
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }
            _flashCoroutine = StartCoroutine(FlashEnemyCoroutine());
        }
    }

    private IEnumerator FlashEnemyCoroutine()
    {
        if (_spriteRenderer == null) yield break;

        _isFlashing = true;
        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(_hitFlashDuration);
        _spriteRenderer.color = _originalColor;
        _isFlashing = false;
        _flashCoroutine = null;
    }

    private void Die(Vector2 deathDirection)
    {
        _isDead = true;
        Debug.Log("Enemy died!");

        // Останавливаем все корутины
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }

        // Останавливаем движение и атаку
        StopMovingImmediately();
        if (_isAttacking)
        {
            EndAttack();
        }

        // Устанавливаем направление смерти для Blend Tree
        _deathDirection = deathDirection.normalized;

        // Запускаем анимацию смерти через Blend Tree
        StartDeathAnimation();

        // Отключаем физику
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            _rb.isKinematic = true;
            _rb.simulated = false;
        }

        // Отключаем коллайдеры
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }

        // Эффект смерти
        if (_deathEffectPrefab != null)
        {
            Instantiate(_deathEffectPrefab, transform.position, Quaternion.identity);
        }

        // Запускаем корутину для уничтожения/управления трупом
        StartCoroutine(DeathSequence());
    }

    private void StartDeathAnimation()
    {
        if (_animator != null)
        {
            // Устанавливаем направление смерти для Blend Tree
            _animator.SetFloat("DeathX", _deathDirection.x);
            _animator.SetFloat("DeathY", _deathDirection.y);

            // Активируем триггер смерти
            _animator.SetTrigger(_deathTriggerName);

            Debug.Log($"Death animation started with direction: {_deathDirection}");
        }
    }

    private IEnumerator DeathSequence()
    {
        // Ждем окончания анимации смерти
        yield return new WaitForSeconds(_deathAnimationDuration);

        // Опционально: делаем врага полупрозрачным
        if (_spriteRenderer != null)
        {
            Color corpseColor = _spriteRenderer.color;
            corpseColor.a = 0.5f;
            _spriteRenderer.color = corpseColor;
        }

        // Ждем пока труп будет виден
        yield return new WaitForSeconds(_corpseDuration);

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

    // Методы из оригинального кода (адаптированы под добавление системы смерти)

    private void CheckEnemyZoneDamage()
    {
        if (_enemyZoneInstance == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _enemyZoneInstance.transform.position,
            _enemyZoneRadius,
            _playerLayer
        );

        bool damageDealt = false;

        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit.CompareTag("Player"))
            {
                if (IsAttackBlocked())
                {
                    HandleBlockedAttack();
                }
                else
                {
                    DealDamageToPlayer();
                }

                damageDealt = true;
                break;
            }
        }

        if (damageDealt)
        {
            _isDealingDamage = false;
        }
    }

    private bool IsAttackBlocked()
    {
        if (!_canBeBlocked || _playerShieldSystem == null)
        {
            return false;
        }

        bool isShielding = _playerShieldSystem.IsShielding();
        bool isShieldActive = _playerShieldSystem.IsShieldActive();

        if (!isShielding || !isShieldActive)
        {
            return false;
        }

        Vector2 attackDirection = (_player.position - transform.position).normalized;
        return _playerShieldSystem.IsAttackBlocked(attackDirection);
    }

    private void HandleBlockedAttack()
    {
        PlayBlockEffect();

        if (_playerShieldSystem != null)
        {
            _playerShieldSystem.TakeStaminaDamage(_staminaDamageToShield);
        }

        ApplyBlockPushback();
        FlashPlayerOnBlock();

        if (_blockSound != null)
        {
            AudioSource.PlayClipAtPoint(_blockSound, transform.position);
        }
    }

    private void PlayBlockEffect()
    {
        if (_blockEffectPrefab != null && _player != null)
        {
            Vector3 effectPosition = _player.position + (Vector3)_attackDirection * 0.5f;
            GameObject effect = Instantiate(_blockEffectPrefab, effectPosition, Quaternion.identity);

            float angle = Mathf.Atan2(_attackDirection.y, _attackDirection.x) * Mathf.Rad2Deg;
            effect.transform.rotation = Quaternion.Euler(0, 0, angle);

            Destroy(effect, 1f);
        }
    }

    private void ApplyBlockPushback()
    {
        if (_rb != null && !_isDead)
        {
            Vector2 pushbackDirection = (transform.position - _player.position).normalized;
            _rb.AddForce(pushbackDirection * _blockPushbackForce, ForceMode2D.Impulse);
        }
    }

    private void FlashPlayerOnBlock()
    {
        if (_playerSprite != null && !_isFlashing)
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }
            _flashCoroutine = StartCoroutine(FlashPlayerBlockCoroutine());
        }
    }

    private IEnumerator FlashPlayerBlockCoroutine()
    {
        if (_playerSprite == null) yield break;

        _isFlashing = true;
        _playerSprite.color = _blockedHitColor;
        yield return new WaitForSeconds(_blockFlashDuration);
        _playerSprite.color = _playerOriginalColor;
        _isFlashing = false;
        _flashCoroutine = null;
    }

    void DealDamageToPlayer()
    {
        if (HealthSystem.Instance != null)
        {
            HealthSystem.Instance.TakeDamage(_attackDamage);
            FlashPlayerOnHit();
        }
    }

    void FlashPlayerOnHit()
    {
        if (_playerSprite != null && !_isFlashing)
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }
            _flashCoroutine = StartCoroutine(FlashPlayerCoroutine());
        }
    }

    private IEnumerator FlashPlayerCoroutine()
    {
        if (_playerSprite == null) yield break;

        _isFlashing = true;
        _playerSprite.color = _playerHitColor;
        yield return new WaitForSeconds(_hitFlashDuration);
        _playerSprite.color = _playerOriginalColor;
        _isFlashing = false;
        _flashCoroutine = null;
    }

    void CheckForPlayer()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        if (distanceToPlayer <= _detectionRange)
        {
            Vector2 directionToPlayer = (_player.position - transform.position).normalized;
            float distanceToPlayerDirect = Vector2.Distance(transform.position, _player.position);

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                directionToPlayer,
                distanceToPlayerDirect,
                _obstacleLayer
            );

            if (hit.collider == null)
            {
                _isPlayerDetected = true;
            }
            else if (hit.collider.CompareTag("Player"))
            {
                _isPlayerDetected = true;
            }
            else
            {
                _isPlayerDetected = false;
            }
        }
        else
        {
            _isPlayerDetected = false;
        }
    }

    bool CanAttack()
    {
        float timeSinceLastAttack = Time.time - _lastAttackTime;
        return timeSinceLastAttack >= _attackCooldown;
    }

    void StartAttack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;

        StopMovingImmediately();

        _attackDirection = (_player.position - transform.position).normalized;
        SetAttackDirection(_attackDirection);

        _animator.SetBool(_attackBoolName, true);

        StartCoroutine(AttackSequence());
    }

    private void CreateEnemyZone()
    {
        if (_enemyZonePrefab != null)
        {
            Vector3 enemyZonePosition = transform.position + (Vector3)_attackDirection * _enemyZoneDistance;
            _enemyZoneInstance = Instantiate(_enemyZonePrefab, enemyZonePosition, Quaternion.identity);
            _enemyZoneRenderer = _enemyZoneInstance.GetComponent<SpriteRenderer>();

            Collider2D collider = _enemyZoneInstance.GetComponent<Collider2D>();
            if (collider == null)
            {
                CircleCollider2D circleCollider = _enemyZoneInstance.AddComponent<CircleCollider2D>();
                circleCollider.radius = _enemyZoneRadius;
                circleCollider.isTrigger = true;
            }
        }
    }

    private void DestroyEnemyZone()
    {
        if (_enemyZoneInstance != null)
        {
            Destroy(_enemyZoneInstance);
            _enemyZoneInstance = null;
            _enemyZoneRenderer = null;
        }
    }

    void SetAttackDirection(Vector2 direction)
    {
        _animator.SetFloat("AttackX", direction.x);
        _animator.SetFloat("AttackY", direction.y);
    }

    IEnumerator AttackSequence()
    {
        yield return new WaitForSeconds(_windupDuration);

        CreateEnemyZone();

        yield return new WaitForSeconds(_attackDelay);

        _isDealingDamage = true;

        yield return new WaitForSeconds(_attackDuration);

        _isDealingDamage = false;
        DestroyEnemyZone();

        yield return new WaitForSeconds(_recoveryDuration);

        EndAttack();
    }

    void EndAttack()
    {
        _isAttacking = false;
        _isDealingDamage = false;
        _animator.SetBool(_attackBoolName, false);

        DestroyEnemyZone();
        ResetPlayerColor();
    }

    private void ResetPlayerColor()
    {
        if (_playerSprite != null)
        {
            _playerSprite.color = _playerOriginalColor;
        }

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        _isFlashing = false;
    }

    void MoveTowardsPlayer()
    {
        _shouldStopMoving = false;
        Vector2 direction = (_player.position - transform.position).normalized;

        if (_rb != null)
        {
            _rb.velocity = direction * _moveSpeed;
        }
    }

    void StopMovingImmediately()
    {
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
        }
        _shouldStopMoving = true;
    }

    void UpdateAnimation()
    {
        if (_animator == null || _isDead) return;

        if (!_isAttacking)
        {
            Vector2 directionToPlayer = (_player.position - transform.position).normalized;
            _animator.SetFloat("Horizontal", directionToPlayer.x);
            _animator.SetFloat("Vertical", directionToPlayer.y);

            float currentSpeed = (_isPlayerDetected && !_shouldStopMoving && !_isAttacking) ? 1f : 0f;
            _animator.SetFloat("Speed", currentSpeed);
        }
        else
        {
            _animator.SetFloat("Horizontal", _attackDirection.x);
            _animator.SetFloat("Vertical", _attackDirection.y);
            _animator.SetFloat("Speed", 0f);
        }
    }

    // Геттеры для доступа из других скриптов
    public bool IsDead() => _isDead;
    public float GetHealthPercent() => _currentHealth / _maxHealth;
    public float GetCurrentHealth() => _currentHealth;
    public float GetMaxHealth() => _maxHealth;

    private void OnDrawGizmosSelected()
    {
        if (_isDead) return;

        Gizmos.color = _isPlayerDetected ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _stoppingDistance);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        if (Application.isPlaying && _enemyZoneInstance != null)
        {
            Gizmos.color = _isDealingDamage ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(_enemyZoneInstance.transform.position, _enemyZoneRadius);
        }
    }
}