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

    private void Start()
    {
        animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _mainCamera = Camera.main;
    }

    void Update()
    {
        Update8DirectionAnimation();
    }

    private void Update8DirectionAnimation()
    {
        Vector2 animationDirection;

        // Определяем направление для анимации
        if (_moveInput.magnitude > 0.1f)
        {
            // При движении - направление от клавиш
            animationDirection = _moveInput.normalized;
        }
        else
        {
            // В покое - направление от мыши (только для анимации, не для поворота)
            Vector3 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            animationDirection = (mousePos - transform.position).normalized;
        }

        // Устанавливаем параметры в аниматор (только анимация, без поворота объекта)
        animator.SetFloat("Horizontal", animationDirection.x);
        animator.SetFloat("Vertical", animationDirection.y);
        animator.SetFloat("Speed", _moveInput.magnitude);

        // Для отладки
        Debug.Log($"Move Input: {_moveInput}, Anim Direction: {animationDirection}, Speed: {_moveInput.magnitude}");
    }

    private void FixedUpdate()
    {
        Move();
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

        // Убеждаемся, что поворот отключен
        _rigidbody.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    // Для визуальной отладки
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            // Направление мыши (красный)
            Vector3 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, (mousePos - transform.position).normalized * 2f);

            // Направление движения (зеленый)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, _moveInput.normalized * 1.5f);

            // Текущее направление анимации (синий)
            Vector2 animDir = new Vector2(
                animator.GetFloat("Horizontal"),
                animator.GetFloat("Vertical")
            ).normalized;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, animDir * 1f);
        }
    }
}