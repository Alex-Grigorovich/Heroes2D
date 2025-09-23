using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _MaxSpeed = 5f;
    [SerializeField] private float _acceleration = 50f;
    [SerializeField] private float _deceleration = 50f;
    [SerializeField] private float _velocityPower = .9f;

    private Vector2 _moveInput;
    private Rigidbody2D _rigidbody;
    private Vector2 _velocity;
    public Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        UpdateMouseDirectionAnimation();
    }

    private void UpdateMouseDirectionAnimation()
    {
        // Позиция мыши в мировых координатах
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        // Вектор от персонажа до мыши (нормализованный)
        Vector2 direction = (mousePos - transform.position).normalized;

        // Для более чётких диагоналей можно немного усилить значения
        float threshold = 0.7f; // Порог для диагональных направлений
        float x = direction.x;
        float y = direction.y;

        // Усиливаем диагональные направления
        if (Mathf.Abs(x) > threshold && Mathf.Abs(y) > threshold)
        {
            x = Mathf.Sign(x) * 1f;
            y = Mathf.Sign(y) * 1f;
        }

        // Передаём нормализованные значения в Blend Tree
        animator.SetFloat("Horizontal", x);
        animator.SetFloat("Vertical", y);

        // Для отладки
        Debug.Log($"Direction X: {x:F2}, Y: {y:F2}");
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
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }
}