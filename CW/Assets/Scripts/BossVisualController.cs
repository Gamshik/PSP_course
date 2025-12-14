using UnityEngine;
using UnityEngine.UI;

public class BossVisualController : MonoBehaviour
{
    public Image HpFill;
    public GameObject Model;

    public void UpdateState(Vector3 pos, float rotY, int hp, int maxHp, bool isActive)
    {
        gameObject.SetActive(isActive);
        if (!isActive) return;

        transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * 10);
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, rotY, 0), Time.deltaTime * 10);

        if (maxHp > 0) HpFill.fillAmount = (float)hp / maxHp;

        if (Camera.main != null)
            HpFill.transform.parent.rotation = Camera.main.transform.rotation;
    }
}