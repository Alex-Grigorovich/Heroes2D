using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCombatSystem : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _MaxSpeed = 5f;
    [SerializeField] private float _acceleration = 50f;
    [SerializeField] private float _deceleration = 50f;
    [SerializeField] private float _velocityPower = .9f;

    [Header("Combat Settings")]
    [SerializeField] private float _attackCooldown = 0.9f;
    [SerializeField] private float _attackDuration = 1f;
    [SerializeField] private int _attackDamage = 25;
    [SerializeField] private float _attackRadius = 1.2f; // Радиус вокруг персонажа
    [SerializeField] private float _attackAngle = 120f; // Угол атаки (градусы)
    [SerializeField] private LayerMask _enemyLayer = 1;

    // Компоненты
    private Rigidbody2D _rigidbody;
    private Animator _animator;
    private Camera _mainCamera;

    // Входные данные
    private Vector2 _moveInput;
    private Vector2 _velocity;

    // Состояние боя
    private bool _isAttacking = false;
    private float _currentAttackCooldown = 0f;
    private Vector2 _attackDirection;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _mainCamera = Camera.main;
    }

    void Update()
    {
        UpdateCooldowns();
        HandleAttackInput();
        UpdateAnimation();
    }

    private void UpdateCooldowns()
    {
        if (_currentAttackCooldown > 0f)
        {
            _currentAttackCooldown -= Time.deltaTime;
        }
    }

    private void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            StartAttack();
        }
    }

    private bool CanAttack()
    {
        return _currentAttackCooldown <= 0f && !_isAttacking;
    }

    private void StartAttack()
    {
        _isAttacking = true;
        _currentAttackCooldown = _attackCooldown;

        // Определяем направление атаки от мыши
        Vector3 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        _attackDirection = (mousePos - transform.position).normalized;

        // Запускаем анимацию атаки
        _animator.SetBool("IsAttacking", true);

        // Начинаем проверку попадания
        StartCoroutine(AttackSequence());

        Debug.Log($"Attack started! Direction: {_attackDirection}");
    }

    private IEnumerator AttackSequence()
    {
        // Ждем пока анимация атаки не достигнет определенного кадра
        yield return new WaitForSeconds(_attackDuration * 0.3f);

        // Наносим урон в момент "удара"
        PerformMeleeAttack();

        // Ждем пока анимация полностью завершится
        yield return new WaitForSeconds(_attackDuration * 0.7f);

        EndAttack();
    }

    private void PerformMeleeAttack()
    {
        // Ищем всех врагов в радиусе
        Collider2D[] allEnemies = Physics2D.OverlapCircleAll(transform.position, _attackRadius, _enemyLayer);

        int hits = 0;
        foreach (Collider2D enemy in allEnemies)
        {
            if (enemy.CompareTag("Enemy") && IsInAttackAngle(enemy.transform.position))
            {
                DealDamageToEnemy(enemy.gameObject);
                hits++;
            }
        }

        Debug.Log($"Melee attack! Found {allEnemies.Length} enemies in radius, hit {hits} in attack angle");
    }

    private bool IsInAttackAngle(Vector3 enemyPosition)
    {
        Vector2 directionToEnemy = (enemyPosition - transform.position).normalized;

        // Вычисляем угол между направлением атаки и направлением к врагу
        float angle = Vector2.Angle(_attackDirection, directionToEnemy);

        // Если враг в пределах угла атаки - попадание
        return angle <= _attackAngle * 0.5f;
    }

    private void DealDamageToEnemy(GameObject enemy)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            Vector2 directionToEnemy = (enemy.transform.position - transform.position).normalized;
            enemyHealth.TakeDamage(_attackDamage, directionToEnemy);
        }
    }

    private void EndAttack()
    {
        _isAttacking = false;
        _animator.SetBool("IsAttacking", false);
    }

    private void UpdateAnimation()
    {
        Vector2 animationDirection;

        if (_isAttacking)
        {
            // Во время атаки используем направление атаки
            animationDirection = _attackDirection;
        }
        else
        {
            // В обычном состоянии используем движение или направление мыши
            if (_moveInput.magnitude > 0.1f)
            {
                animationDirection = _moveInput.normalized;
            }
            else
            {
                Vector3 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                mousePos.z = 0;
                animationDirection = (mousePos - transform.position).normalized;
            }
        }

        // Обновляем параметры аниматора
        _animator.SetFloat("Horizontal", animationDirection.x);
        _animator.SetFloat("Vertical", animationDirection.y);
        _animator.SetFloat("Speed", _moveInput.magnitude);
    }

    private void FixedUpdate()
    {
        if (!_isAttacking)
        {
            Move();
        }
        else
        {
            _velocity = Vector2.zero;
            _rigidbody.velocity = Vector2.zero;
        }
    }

    private void Move()
    {
        Vector2 targetVelocity = _moveInput * _MaxSpeed;
        Vector2 velocityDiff = targetVelocity - _velocity;
        float accelerateRate = (Mathf.Abs(targetVelocity.magnitude) > 0.01f) ? _acceleration : _deceleration;

        Vector2 movement = velocityDiff * (accelerateRate * Time.fixedDeltaTime);

        _velocity += movement;
        _velocity = Vector2.ClampMagnitude(_velocity, _MaxSpeed);
        _velocity *= MathF.Pow(1f - _velocityPower, Time.fixedDeltaTime);

        _rigidbody.MovePosition(_rigidbody.position + _velocity * Time.fixedDeltaTime);

        // Блокировка вращения
        _rigidbody.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
    }

    // Input System events
    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed && CanAttack())
        {
            StartAttack();
        }
    }

    // Визуализация в редакторе
    private void OnDrawGizmosSelected()
    {
        // Радиус атаки
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRadius);

        // Угол атаки
        if (Application.isPlaying && _isAttacking)
        {
            Gizmos.color = Color.red;
            DrawAttackAngleGizmo();
        }
    }

    private void DrawAttackAngleGizmo()
    {
        float halfAngle = _attackAngle * 0.5f * Mathf.Deg2Rad;

        Vector2 leftDir = new Vector2(
            _attackDirection.x * Mathf.Cos(halfAngle) - _attackDirection.y * Mathf.Sin(halfAngle),
            _attackDirection.x * Mathf.Sin(halfAngle) + _attackDirection.y * Mathf.Cos(halfAngle)
        );

        Vector2 rightDir = new Vector2(
            _attackDirection.x * Mathf.Cos(halfAngle) + _attackDirection.y * Mathf.Sin(halfAngle),
            -_attackDirection.x * Mathf.Sin(halfAngle) + _attackDirection.y * Mathf.Cos(halfAngle)
        );

        Gizmos.DrawLine(transform.position, transform.position + (Vector3)leftDir * _attackRadius);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)rightDir * _attackRadius);
        Gizmos.DrawLine(transform.position + (Vector3)leftDir * _attackRadius,
                        transform.position + (Vector3)rightDir * _attackRadius);
    }
}