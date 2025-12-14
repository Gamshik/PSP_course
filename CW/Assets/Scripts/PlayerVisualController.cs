using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerVisualController : MonoBehaviour
{
    [Header("Visuals")]
    public Renderer BodyRenderer;
    public Renderer SwordRenderer;

    public GameObject SwordPivot;

    public Image HpBarFill;
    public Canvas HpCanvas;

    private Color _baseColor;
    private bool _isDead = false;
    private Transform _cameraTransform;

    private void Start()
    {
        if (Camera.main != null) _cameraTransform = Camera.main.transform;
    }

    public void Initialize(Color color)
    {
        _baseColor = color;
        BodyRenderer.material.color = color;
        SwordRenderer.material.color = color;
        SwordPivot.SetActive(false); 
    }

    public void UpdateState(Vector3 pos, Quaternion rot, int currentHp, int maxHp, bool isDead)
    {
        transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * 15);
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, Time.deltaTime * 20);

        float percent = (float)currentHp / maxHp;
        HpBarFill.fillAmount = percent;

        if (HpCanvas != null && _cameraTransform != null)
            HpCanvas.transform.rotation = _cameraTransform.rotation;

        if (_isDead != isDead)
        {
            _isDead = isDead;
            BodyRenderer.material.color = isDead ? Color.gray : _baseColor;
            if (isDead) SwordPivot.SetActive(false);
        }
    }

    public void PlayMeleeAnim(float duration)
    {
        if (SwordPivot.activeSelf) return;
        if (gameObject.activeInHierarchy) StartCoroutine(MeleeRoutine(duration));
    }

    private IEnumerator MeleeRoutine(float duration)
    {
        SwordPivot.SetActive(true);
        float time = 0;

        Quaternion startRotation = transform.rotation;

        while (time < duration)
        {
            time += Time.deltaTime;
            float progress = time / duration;

            float angle = Mathf.Lerp(-60, 60, progress);

            Quaternion swingRotation = Quaternion.Euler(0, angle, 0);

            SwordPivot.transform.rotation = startRotation * swingRotation;

            yield return null;
        }
        SwordPivot.SetActive(false);
    }
    private float _lastMeleeTime = -1f;

    public void CheckMeleeAnimation(float currentTimer, float maxCooldown, float animDuration)
    {
        if (currentTimer > maxCooldown - 0.1f && currentTimer != _lastMeleeTime)
        {
            _lastMeleeTime = currentTimer; 
            PlayMeleeAnim(animDuration);
        }
    }
    private float _lastMeleeTimer = 0f;
    public void UpdateMeleeTimer(float currentTimer, float animDuration)
    {
        if (currentTimer > _lastMeleeTimer + 0.1f)
        {
            StopAllCoroutines();
            if (gameObject.activeInHierarchy) StartCoroutine(MeleeRoutine(animDuration));
        }

        _lastMeleeTimer = currentTimer;
    }
}