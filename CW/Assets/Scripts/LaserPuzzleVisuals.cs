using System.Collections.Generic;
using UnityEngine;

public class LaserPuzzleVisuals : MonoBehaviour
{
    [Header("Prefabs")]
    public LineRenderer LaserBeamPrefab;
    public PuzzlePlateVisual PlatePrefab;

    [Header("Containers")]
    public Transform LasersContainer;
    public Transform PlatesContainer;

    private List<LineRenderer> _lasers = new List<LineRenderer>();
    private List<PuzzlePlateVisual> _plates = new List<PuzzlePlateVisual>();

    private float[] _targetAngles;
    private float[] _currentAngles;

    private void Update()
    {
        if (_targetAngles != null && _currentAngles != null)
        {
            for (int i = 0; i < _lasers.Count; i++)
            {
                if (i >= _targetAngles.Length) break;

                _currentAngles[i] = Mathf.LerpAngle(_currentAngles[i], _targetAngles[i], Time.deltaTime * 10f);

                _lasers[i].transform.localRotation = Quaternion.Euler(0, _currentAngles[i], 0);
            }
        }
    }

    public void UpdateState(float[] laserAngles, float[] platesProgress, bool[] playersOnPlates, Vector3[] platePositions)
    {
        gameObject.SetActive(true);

        AdjustCount(_lasers, laserAngles.Length, LaserBeamPrefab, LasersContainer);

        if (_targetAngles == null || _targetAngles.Length != laserAngles.Length)
        {
            _targetAngles = new float[laserAngles.Length];
            _currentAngles = new float[laserAngles.Length];

            for (int k = 0; k < laserAngles.Length; k++)
            {
                _targetAngles[k] = laserAngles[k];
                _currentAngles[k] = laserAngles[k];
            }
        }
        else
        {
            for (int k = 0; k < laserAngles.Length; k++) _targetAngles[k] = laserAngles[k];
        }

        for (int i = 0; i < _lasers.Count; i++)
        {
            _lasers[i].gameObject.SetActive(true);
        }

        AdjustCount(_plates, platesProgress.Length, PlatePrefab, PlatesContainer);
        for (int i = 0; i < platesProgress.Length; i++)
        {
            var plate = _plates[i];
            plate.gameObject.SetActive(true);
            if (platePositions != null && i < platePositions.Length)
                plate.transform.position = platePositions[i];
            plate.UpdateState(platesProgress[i], playersOnPlates[i]);
        }
    }

    private void AdjustCount<T>(List<T> list, int targetCount, T prefab, Transform parent) where T : Component
    {
        while (list.Count < targetCount)
        {
            T obj = Instantiate(prefab, parent);
            obj.transform.localPosition = Vector3.zero;
            list.Add(obj);
        }
        for (int i = 0; i < list.Count; i++)
        {
            if (i >= targetCount) list[i].gameObject.SetActive(false);
        }
    }
}