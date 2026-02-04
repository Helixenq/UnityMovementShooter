using UnityEngine;
using TMPro;

public class SpeedUI : MonoBehaviour
{
    [SerializeField] Rigidbody targetRb;
    [SerializeField] TMP_Text text;
    [SerializeField] bool showHorizontalOnly = true;

    void Reset()
    {
        text = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (!targetRb || !text) return;

        Vector3 v = targetRb.linearVelocity;
        float speed = showHorizontalOnly ? new Vector3(v.x, 0f, v.z).magnitude : v.magnitude;

        text.text = $"Speed: {speed:0.00}";
    }
}
