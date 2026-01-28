using UnityEngine;
using System.Collections;

public class EnemyController8Directions : MonoBehaviour
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
    [SerializeField] private string _attackBoolName = "attack";

    // Компоненты
    private Transform _player;
    private Rigidbody2D _rb;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private SpriteRenderer _playerSprite;
    private Color _playerOriginalColor;
    private Color _originalColor;
    private EnemyHealth _enemyHealth;

    // Системы игрока
    private PlayerShieldSystem _playerShieldSystem;
    private HealthSystem _playerHealthSystem;

    // Состояние врага
    private bool _isDead = false;
    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private bool _shouldStopMoving = false;
    private float _lastAttackTime = 0f;
    private Vector2 _attackDirection = Vector2.down;
    private Vector2 _lastDirectionToPlayer = Vector2.down;

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
        _enemyHealth = GetComponent<EnemyHealth>();

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        FindAndSetupPlayer();

        // Отладочная информация аниматора
        if (_animator != null)
        {
            Debug.Log("=== ENEMY CONTROLLER STARTED ===");
            Debug.Log($"Animator: {_animator.name}");
        }
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
            Debug.Log($"HealthSystem: {(_playerHealthSystem != null ? "Found" : "NOT FOUND!")}");
            Debug.Log($"ShieldSystem: {(_playerShieldSystem != null ? "Found" : "Not found")}");

            if (_playerHealthSystem == null)
            {
                Debug.LogError("HealthSystem.Instance is null!");
            }
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
        // Проверяем, не умер ли враг
        if (_enemyHealth != null)
        {
            _isDead = _enemyHealth.IsDead();
        }

        if (_isDead)
        {
            // Если враг мертв, отключаем все действия
            StopMovingImmediately();
            if (_isAttacking)
            {
                EndAttack();
            }
            return;
        }

        if (_player == null)
        {
            FindAndSetupPlayer();
            if (_player == null) return;
        }

        CheckForPlayer();

        // Обновляем направление к игроку
        if (_player != null)
        {
            _lastDirectionToPlayer = (_player.position - transform.position).normalized;
        }

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

    // Метод вызывается при смерти врага
    public void OnDeath()
    {
        Debug.Log("EnemyController: OnDeath called");
        StopAllCoroutines();
        StopMovingImmediately();
        EndAttack();

        // Отключаем скрипт
        enabled = false;
    }

    // Метод для получения последнего направления атаки
    public Vector2 GetLastAttackDirection()
    {
        return _attackDirection;
    }

    // Метод для получения направления к игроку
    public Vector2 GetDirectionToPlayer()
    {
        return _lastDirectionToPlayer;
    }

    // Метод для получения направления движения
    public Vector2 GetMovementDirection()
    {
        if (_rb != null && _rb.velocity != Vector2.zero)
        {
            return _rb.velocity.normalized;
        }
        return _lastDirectionToPlayer;
    }

    private void CheckEnemyZoneDamage()
    {
        if (_enemyZoneInstance == null)
            return;

        // Проверяем столкновение с игроком
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
                Debug.Log("Player detected in enemy zone!");

                // Проверяем блокировку перед нанесением урона
                if (IsAttackBlocked())
                {
                    Debug.Log("Attack would be blocked by shield!");
                    HandleBlockedAttack();
                }
                else
                {
                    Debug.Log("Attack not blocked, dealing damage");
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
            Debug.Log($"CanBeBlocked: {_canBeBlocked}, ShieldSystem: {_playerShieldSystem != null}");
            return false;
        }

        bool isShielding = _playerShieldSystem.IsShielding();
        bool isShieldActive = _playerShieldSystem.IsShieldActive();

        Debug.Log($"Shield check - IsShielding: {isShielding}, IsShieldActive: {isShieldActive}");

        if (!isShielding || !isShieldActive)
        {
            Debug.Log("Shield is not active");
            return false;
        }

        Vector2 attackDirection = (_player.position - transform.position).normalized;
        Debug.Log($"Attack direction: {attackDirection}");

        bool isBlocked = _playerShieldSystem.IsAttackBlocked(attackDirection);
        Debug.Log($"IsAttackBlocked result: {isBlocked}");

        return isBlocked;
    }

    private void HandleBlockedAttack()
    {
        Debug.Log("🛡️ Атака заблокирована щитом!");

        PlayBlockEffect();

        if (_playerShieldSystem != null)
        {
            _playerShieldSystem.TakeStaminaDamage(_staminaDamageToShield);
            float staminaPercent = _playerShieldSystem.GetStaminaPercent();
            Debug.Log($"🛡️ Стамина щита: {staminaPercent:P0}");

            if (staminaPercent <= 0)
            {
                Debug.Log("💥 Щит сломан!");
            }
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
            Debug.Log($"Dealing {_attackDamage} damage to player");
            HealthSystem.Instance.TakeDamage(_attackDamage);
            FlashPlayerOnHit();
            Debug.Log($"💥 Enemy dealt {_attackDamage} damage!");
        }
        else
        {
            Debug.LogError("HealthSystem.Instance is null!");
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
        if (_isDead) return;

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

            Debug.DrawRay(transform.position, directionToPlayer * distanceToPlayerDirect, Color.red);

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
        if (_isDead) return false;

        float timeSinceLastAttack = Time.time - _lastAttackTime;
        return timeSinceLastAttack >= _attackCooldown;
    }

    void StartAttack()
    {
        if (_isDead) return;

        Debug.Log("Starting attack");
        _isAttacking = true;
        _lastAttackTime = Time.time;

        StopMovingImmediately();

        _attackDirection = (_player.position - transform.position).normalized;
        SetAttackDirection(_attackDirection);

        _animator.SetBool(_attackBoolName, true);
        _animator.Update(0f);

        StartCoroutine(AttackSequence());
    }

    private void CreateEnemyZone()
    {
        if (_enemyZonePrefab != null && !_isDead)
        {
            Vector3 enemyZonePosition = transform.position + (Vector3)_attackDirection * _enemyZoneDistance;
            _enemyZoneInstance = Instantiate(_enemyZonePrefab, enemyZonePosition, Quaternion.identity);
            _enemyZoneRenderer = _enemyZoneInstance.GetComponent<SpriteRenderer>();

            // Проверяем наличие коллайдера
            Collider2D collider = _enemyZoneInstance.GetComponent<Collider2D>();
            if (collider == null)
            {
                Debug.LogWarning("EnemyZone prefab has no Collider2D! Adding CircleCollider2D...");
                CircleCollider2D circleCollider = _enemyZoneInstance.AddComponent<CircleCollider2D>();
                circleCollider.radius = _enemyZoneRadius;
                circleCollider.isTrigger = true;
            }

            Debug.Log($"EnemyZone created at position: {enemyZonePosition}");
        }
    }

    private void DestroyEnemyZone()
    {
        if (_enemyZoneInstance != null)
        {
            Destroy(_enemyZoneInstance);
            _enemyZoneInstance = null;
            _enemyZoneRenderer = null;
            Debug.Log("EnemyZone destroyed");
        }
    }

    void SetAttackDirection(Vector2 direction)
    {
        _animator.SetFloat("AttackX", direction.x);
        _animator.SetFloat("AttackY", direction.y);
    }

    IEnumerator AttackSequence()
    {
        if (_isDead)
        {
            EndAttack();
            yield break;
        }

        Debug.Log("Windup phase started");
        yield return new WaitForSeconds(_windupDuration);

        if (_isDead)
        {
            EndAttack();
            yield break;
        }

        Debug.Log("Attack delay - warning phase");
        CreateEnemyZone();

        yield return new WaitForSeconds(_attackDelay);

        if (_isDead)
        {
            DestroyEnemyZone();
            EndAttack();
            yield break;
        }

        _isDealingDamage = true;
        Debug.Log("START dealing damage phase with EnemyZone");

        yield return new WaitForSeconds(_attackDuration);

        _isDealingDamage = false;
        Debug.Log("END dealing damage phase");

        DestroyEnemyZone();

        yield return new WaitForSeconds(_recoveryDuration);

        EndAttack();
    }

    void EndAttack()
    {
        Debug.Log("Ending attack");
        _isAttacking = false;
        _isDealingDamage = false;
        _animator.SetBool(_attackBoolName, false);
        _animator.Update(0f);

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
        if (_isDead) return;

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
    public float GetAttackDamage() => _attackDamage;

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

        if (Application.isPlaying && _isAttacking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)_attackDirection * _attackRange);

            if (_enemyZoneInstance != null)
            {
                Gizmos.color = _isDealingDamage ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(_enemyZoneInstance.transform.position, 0.1f);
                Gizmos.DrawLine(transform.position, _enemyZoneInstance.transform.position);
            }
        }
    }
}