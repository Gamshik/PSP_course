using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target; 
    public Vector3 Offset;   
    public float SmoothSpeed = 5f;

    private void LateUpdate()
    {
        if (Target == null) return;

        Vector3 desiredPosition = Target.position + Offset;

        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, SmoothSpeed * Time.deltaTime);

        transform.position = smoothedPosition;
    }
}