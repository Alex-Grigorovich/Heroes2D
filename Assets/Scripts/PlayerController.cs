using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController8Directions : MonoBehaviour
{
    [SerializeField] private float _MaxSpeed = 5f;
    [SerializeField] private float _acceleration = 50f;
    [SerializeField] private float _deceleration = 50f;
    [SerializeField] private float _velocityPower = .9f;

    private Vector2 _moveInput;
    private Rigidbody2D _rigidbody;
    private Vector2 _velocity;
    public Animator animator;
    private Camera _mainCamera;

    // Переменные для анимации удара
    private bool _isAttacking = false;
    private float _attackCooldown = 0f;
    private const float ATTACK_COOLDOWN = 0.5f;
    private float _attackTimer = 0f;
    private const float ATTACK_DURATION = 0.8f;

    // Направление атаки
    private Vector2 _attackDirection;

    // Блокировка движения во время атаки
    private bool _movementLocked = false;
    private Vector2 _storedMoveInput;

    private void Start()
    {
        animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _mainCamera = Camera.main;

        // Очищаем все события анимации
        CleanAllAnimationEvents();
    }

    // Метод для очистки всех Animation Events
    private void CleanAllAnimationEvents()
    {
#if UNITY_EDITOR
        try
        {
            // Находим все анимации в аниматоре
            RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
            if (runtimeController != null)
            {
                foreach (AnimationClip clip in runtimeController.animationClips)
                {
                    // Очищаем все события для этой анимации
                    UnityEditor.AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                    Debug.Log($"Cleared events from: {clip.name}");
                }
            }
            Debug.Log("All animation events cleared successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not clear animation events: " + e.Message);
        }
#endif
    }

    void Update()
    {
        UpdateAttackCooldown();
        HandleAttackInput();
        UpdateAttackTimer();
        Update8DirectionAnimation();
    }

    private void UpdateAttackTimer()
    {
        if (_isAttacking)
        {
            _attackTimer += Time.deltaTime;

            if (_attackTimer >= ATTACK_DURATION)
            {
                EndAttack();
            }
        }
    }

    private void UpdateAttackCooldown()
    {
        if (_attackCooldown > 0f)
        {
            _attackCooldown -= Time.deltaTime;
        }
    }

    private void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0) && _attackCooldown <= 0f && !_isAttacking)
        {
            StartAttack();
        }
    }

    private void StartAttack()
    {
        _isAttacking = true;
        _movementLocked = true;
        _attackCooldown = ATTACK_COOLDOWN;
        _attackTimer = 0f;

        _storedMoveInput = _moveInput;
        _moveInput = Vector2.zero;

        Vector3 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        _attackDirection = (mousePos - transform.position).normalized;

        animator.SetFloat("AttackHorizontal", _attackDirection.x);
        animator.SetFloat("AttackVertical", _attackDirection.y);
        animator.SetBool("IsAttacking", true);

        _velocity = Vector2.zero;

        Debug.Log($"Attack started! Direction: {_attackDirection}");
    }

    private void EndAttack()
    {
        _isAttacking = false;
        _movementLocked = false;
        _moveInput = _storedMoveInput;
        animator.SetBool("IsAttacking", false);
        _attackTimer = 0f;
        Debug.Log("Attack ended!");
    }

    private void Update8DirectionAnimation()
    {
        Vector2 animationDirection;

        if (_isAttacking)
        {
            animationDirection = _attackDirection;
        }
        else
        {
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

        animator.SetFloat("Horizontal", animationDirection.x);
        animator.SetFloat("Vertical", animationDirection.y);

        float speedValue = (_isAttacking || _movementLocked) ? 0f : _moveInput.magnitude;
        animator.SetFloat("Speed", speedValue);
    }

    private void FixedUpdate()
    {
        if (!_isAttacking && !_movementLocked)
        {
            Move();
        }
        else
        {
            _velocity = Vector2.zero;
        }
    }

    private void Move()
    {
        Vector2 targetVelosity = _moveInput * _MaxSpeed;
        Vector2 velocityDiff = targetVelosity - _velocity;
        float accelerateRate = (Mathf.Abs(targetVelosity.magnitude) > 0.01f) ? _acceleration : _deceleration;

        Vector2 movement = velocityDiff * (accelerateRate * Time.fixedDeltaTime);

        _velocity += movement;
        _velocity = Vector2.ClampMagnitude(_velocity, _MaxSpeed);
        _velocity *= MathF.Pow(1f - _velocityPower, Time.fixedDeltaTime);

        _rigidbody.MovePosition(_rigidbody.position + _velocity * Time.fixedDeltaTime);

        _rigidbody.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (_movementLocked)
        {
            _storedMoveInput = context.ReadValue<Vector2>();
            return;
        }

        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed && _attackCooldown <= 0f && !_isAttacking)
        {
            StartAttack();
        }
    }
}