using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{

    [SerializeField] private Transform _target; // цель за которой следует камера
    [SerializeField] private float _smoothTime = 0.3f; // время сглаживания плавного движения камеры
    [SerializeField] private float _rotationSpeed = 5f; // скорость вращения камеры
    [SerializeField] private float _maxRotationAngle = 10f; //максимальный угол поворота камеры
    [SerializeField] private Vector3 _offset = new Vector3(0, 0, -10); // смещение камеры относительно цели

    private Vector3 _velocity = Vector3.zero;
    private float _currentRotation = 0f; // текущий угол поворота камеры

    private void LateUpdate()
    {
        if(_target == null)
        {
            Debug.LogWarning("Таргет не установлен");
            return;
        }

        FollowTarget();
        RotateCamera();

    }

    

    private void FollowTarget()
    {
        Vector3 targetPosition = _target.position + _offset; // целевая позиция камеры
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime); // перемещение камеры к целевой позиции
    }

    private void RotateCamera()
    {
        Vector3 mousePosition = Input.mousePosition;
        float screenCenterX = Screen.width / 2f;
        float mouseDeltaX = (mousePosition.x - screenCenterX) / screenCenterX;

        float targetRotation = mouseDeltaX * _maxRotationAngle;
        _currentRotation = Mathf.Lerp(_currentRotation,  targetRotation, Time.deltaTime * _rotationSpeed);

        transform.rotation = Quaternion.Euler(0, 0, _currentRotation);



    }

}
