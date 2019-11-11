using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Heat Map Object Container", menuName = "Heat Map Object Container")]
public class HeatMapObjectContainer : ScriptableObject {
	public List<Transform> objects = new List<Transform>();
}
