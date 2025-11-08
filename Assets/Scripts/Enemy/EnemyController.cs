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

    [Header("Attack Timing")]
    [SerializeField] private float _windupDuration = 0.3f;
    [SerializeField] private float _attackDelay = 0.6f; // Пауза перед уроном
    [SerializeField] private float _attackDuration = 0.2f;
    [SerializeField] private float _recoveryDuration = 0.5f;

    [Header("Enemy Zone Settings")]
    [SerializeField] private GameObject _enemyZonePrefab;
    [SerializeField] private float _enemyZoneDistance = 0.7f;
    [SerializeField] private float _enemyZoneShowDuration = 0.8f;
    [SerializeField] private float _enemyZoneRadius = 0.5f;

    [Header("Visual Effects")]
    [SerializeField] private Color _playerHitColor = Color.red;
    [SerializeField] private float _hitFlashDuration = 0.4f;

    private Transform _player;
    private Rigidbody2D _rb;
    private Animator _animator;
    private SpriteRenderer _playerSprite;
    private Color _playerOriginalColor;

    private bool _isPlayerDetected = false;
    private bool _isAttacking = false;
    private bool _shouldStopMoving = false;
    private float _lastAttackTime = 0f;
    private Vector2 _attackDirection = Vector2.down;

    // Визуализация атаки врага
    private GameObject _enemyZoneInstance;
    private SpriteRenderer _enemyZoneRenderer;
    private bool _isDealingDamage = false;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
            _playerSprite = _player.GetComponent<SpriteRenderer>();
            if (_playerSprite != null)
            {
                _playerOriginalColor = _playerSprite.color;
            }
        }

        if (_rb != null)
        {
            _rb.gravityScale = 0f;
            _rb.drag = 10f;
            _rb.angularDrag = 10f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // Инициализируем Enemy Zone
        InitializeEnemyZone();
    }

    private void InitializeEnemyZone()
    {
        if (_enemyZonePrefab != null)
        {
            _enemyZoneInstance = Instantiate(_enemyZonePrefab, transform.position, Quaternion.identity);
            _enemyZoneRenderer = _enemyZoneInstance.GetComponent<SpriteRenderer>();

            if (_enemyZoneRenderer != null)
            {
                // Сразу скрываем зону атаки по умолчанию
                _enemyZoneRenderer.enabled = false;
                Debug.Log("✅ EnemyZone initialized and hidden");
            }
            else
            {
                Debug.LogWarning("❌ EnemyZone prefab doesn't have SpriteRenderer component!");
            }
        }
        else
        {
            Debug.LogWarning("❌ EnemyZone prefab is not assigned!");
        }
    }

    void Update()
    {
        if (_player == null) return;

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

        // Проверяем попадание по игроку в реальном времени, когда наносится урон
        if (_isDealingDamage && _enemyZoneRenderer != null && _enemyZoneRenderer.enabled)
        {
            CheckEnemyZoneDamage();
        }

        UpdateAnimation();
    }

    private void CheckEnemyZoneDamage()
    {
        if (_enemyZoneInstance == null || _enemyZoneRenderer == null || !_enemyZoneRenderer.enabled)
            return;

        // Используем более надежный метод обнаружения
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _enemyZoneInstance.transform.position,
            _enemyZoneRadius,
            _playerLayer
        );

        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit.CompareTag("Player"))
            {
                DealDamageToPlayer();
                _isDealingDamage = false;
                break; // Наносим урон только одному игроку
            }
        }
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

            Debug.DrawRay(transform.position, directionToPlayer * distanceToPlayerDirect, Color.red);

            if (hit.collider == null)
            {
                _isPlayerDetected = true;
                Debug.Log("🎯 Player detected - no obstacles");
            }
            else if (hit.collider.CompareTag("Player"))
            {
                _isPlayerDetected = true;
                Debug.Log("🎯 Player detected directly");
            }
            else
            {
                _isPlayerDetected = false;
                Debug.Log($"🚫 Player blocked by: {hit.collider.name}");
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

        _animator.SetBool("attack", true);
        _animator.Update(0f);

        StartCoroutine(AttackSequence());
    }

    private void ShowEnemyZone()
    {
        if (_enemyZoneRenderer != null && _enemyZoneInstance != null)
        {
            // Вычисляем позицию зоны атаки ВОКРУГ врага
            Vector3 enemyZonePosition = transform.position + (Vector3)_attackDirection * _enemyZoneDistance;

            // Устанавливаем позицию
            _enemyZoneInstance.transform.position = enemyZonePosition;

            // Показываем зону атаки
            _enemyZoneRenderer.enabled = true;

            Debug.Log($"🎯 EnemyZone shown at position: {enemyZonePosition}");
        }
        else
        {
            Debug.LogWarning("❌ EnemyZone components are not properly initialized!");
        }
    }

    private IEnumerator HideEnemyZoneAfterDelay()
    {
        yield return new WaitForSeconds(_enemyZoneShowDuration);

        if (_enemyZoneRenderer != null)
        {
            _enemyZoneRenderer.enabled = false;
            Debug.Log("🎯 EnemyZone hidden");
        }
    }

    void SetAttackDirection(Vector2 direction)
    {
        _animator.SetFloat("AttackX", direction.x);
        _animator.SetFloat("AttackY", direction.y);
    }

    IEnumerator AttackSequence()
    {
        // Фаза 1: Замах (подготовка к атаке)
        Debug.Log("⚡ Windup phase started");
        yield return new WaitForSeconds(_windupDuration);

        // Фаза 2: Пауза перед ударом (предупреждение для игрока)
        Debug.Log("⏰ Attack delay - warning phase");
        ShowEnemyZone(); // Показываем зону атаки как предупреждение
        yield return new WaitForSeconds(_attackDelay);

        // Фаза 3: Удар - начинаем наносить урон через EnemyZone
        _isDealingDamage = true;
        Debug.Log("💥 START dealing damage phase with EnemyZone");

        yield return new WaitForSeconds(_attackDuration);

        // Заканчиваем наносить урон
        _isDealingDamage = false;
        Debug.Log("💥 END dealing damage phase");

        // Скрываем зону атаки
        StartCoroutine(HideEnemyZoneAfterDelay());

        // Фаза 4: Восстановление
        yield return new WaitForSeconds(_recoveryDuration);

        EndAttack();
    }

    void DealDamageToPlayer()
    {
        if (HealthSystem.Instance != null)
        {
            HealthSystem.Instance.TakeDamage(_attackDamage);
            StartCoroutine(FlashPlayerOnHit());
            Debug.Log($"💥 Enemy dealt {_attackDamage} damage via EnemyZone!");
        }
    }

    IEnumerator FlashPlayerOnHit()
    {
        if (_playerSprite != null)
        {
            Color originalColor = _playerSprite.color;
            _playerSprite.color = _playerHitColor;
            yield return new WaitForSeconds(_hitFlashDuration);
            _playerSprite.color = originalColor;
        }
    }

    void EndAttack()
    {
        _isAttacking = false;
        _isDealingDamage = false;
        _animator.SetBool("attack", false);
        _animator.Update(0f);
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
        if (_animator == null) return;

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isPlayerDetected ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _stoppingDistance);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        // Enemy Zone визуализация
        if (Application.isPlaying && _enemyZoneInstance != null && _enemyZoneRenderer != null && _enemyZoneRenderer.enabled)
        {
            Gizmos.color = _isDealingDamage ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(_enemyZoneInstance.transform.position, _enemyZoneRadius);
        }

        if (Application.isPlaying && _isAttacking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)_attackDirection * _attackRange);

            // Позиция Enemy Zone
            if (_enemyZoneInstance != null && _enemyZoneRenderer != null && _enemyZoneRenderer.enabled)
            {
                Gizmos.color = _isDealingDamage ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(_enemyZoneInstance.transform.position, 0.1f);
                Gizmos.DrawLine(transform.position, _enemyZoneInstance.transform.position);
            }
        }
    }


    
}