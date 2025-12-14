using UnityEngine;

public class PuzzleCubeVisual : MonoBehaviour
{
    public Renderer MeshRenderer;
    public Color GoldColor = new Color(1f, 0.84f, 0f); 
    private Color _originalColor;
    private bool _hasInitialized = false;

    private void Awake()
    {
        if (MeshRenderer == null) MeshRenderer = GetComponent<Renderer>();
        if (MeshRenderer != null) _originalColor = MeshRenderer.material.color;
    }

    public void UpdateState(Vector3 pos, bool isActive, bool isCompleted)
    {
        if (gameObject.activeSelf != isActive) gameObject.SetActive(isActive);

        if (isActive)
        {
            transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * 10);

            if (MeshRenderer != null)
            {
                if (isCompleted)
                    MeshRenderer.material.color = GoldColor;
                else
                    MeshRenderer.material.color = _originalColor;
            }
        }
    }
}