using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro _textMesh;
    private float _disappearTimer;
    private Color _textColor;
    private Vector3 _moveVector;

    public static DamagePopup Create(Vector3 position, int damageAmount, bool isCritical)
    {
        GameObject damagePopupGO = new GameObject("DamagePopup");
        DamagePopup damagePopup = damagePopupGO.AddComponent<DamagePopup>();
        damagePopup.Setup(damageAmount, isCritical);
        damagePopup.transform.position = position;

        return damagePopup;
    }

    void Awake()
    {
        _textMesh = gameObject.AddComponent<TextMeshPro>();
        _textMesh.alignment = TextAlignmentOptions.Center;
        _textMesh.fontSize = 4f;
        _textMesh.sortingOrder = 100;
    }

    public void Setup(int damage, bool isCritical)
    {
        _textMesh.text = damage.ToString();

        if (isCritical)
        {
            _textMesh.color = Color.yellow;
            _textMesh.fontSize = 5f;
            _textMesh.text += "!";
        }
        else
        {
            _textMesh.color = Color.white;
        }

        _textColor = _textMesh.color;
        _disappearTimer = 1f;
        _moveVector = new Vector3(0.7f, 1) * 2f;
    }

    void Update()
    {
        transform.position += _moveVector * Time.deltaTime;
        _moveVector -= _moveVector * 8f * Time.deltaTime;

        _disappearTimer -= Time.deltaTime;
        if (_disappearTimer < 0)
        {
            float disappearSpeed = 3f;
            _textColor.a -= disappearSpeed * Time.deltaTime;
            _textMesh.color = _textColor;

            if (_textColor.a < 0)
            {
                Destroy(gameObject);
            }
        }
    }
}