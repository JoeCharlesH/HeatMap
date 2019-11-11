using UnityEngine;

public class HeatMapAgent : MonoBehaviour {
    public HeatMapObjectContainer container;

    private void OnEnable() {
        container.objects.Add(this.transform);
    }

    private void OnDisable() {
        container.objects.Remove(this.transform);
    }
}