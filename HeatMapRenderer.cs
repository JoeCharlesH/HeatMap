using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using UnityEngine;

public class HeatMapRenderer : MonoBehaviour {
	[System.Serializable]
	public class MapChunk {
		public Dictionary<int, double> map = new Dictionary<int, double>();
		public double max = 0;

		public double this[int i] {
			get {
				double val = 0;
				if (map.TryGetValue(i, out val)) return val;
				else return 0;
			}
			set {
				max = System.Math.Max(max, value);
				map[i] = value;
			}
		}

		public int length { get{ return map.Count; }}

		public void Clear() {
			map.Clear();
		}

		public MapChunk(int capacity) {
			map = new Dictionary<int, double>(capacity);
		}

		public MapChunk(string path) {
			byte[] mapBytes = System.IO.File.ReadAllBytes(path);
			int count = (mapBytes.Length - sizeof(double)) / (sizeof(int) + sizeof(double));
			int posLen = sizeof(int) * count;
			int valLen = sizeof(double) * count;

			max = System.BitConverter.ToDouble(mapBytes, 0);

			int[] keys = new int[count];
			double[] vals = new double[count];

			System.Buffer.BlockCopy(mapBytes, sizeof(double), keys, 0, posLen);
			System.Buffer.BlockCopy(mapBytes, sizeof(double) + posLen, vals, 0, valLen);

			map = Enumerable.Range(0, count).ToDictionary(i => keys[i], i => vals[i]);
		}

		public static MapChunk Copy(MapChunk original) {
			MapChunk clone = new MapChunk(0);
			clone.max = original.max;
			clone.map = original.map.ToDictionary(entry => entry.Key, entry => entry.Value);
			return clone;
		}

		public void Save(string dirName, string fileName) {
			int maxLen = sizeof(double);
			int posLen = length * sizeof(int);
			int valLen = length * sizeof(double);

			byte[] result = new byte[maxLen + posLen + valLen];

			System.Buffer.BlockCopy(System.BitConverter.GetBytes(max), 0, result, 0, maxLen);
			System.Buffer.BlockCopy(map.Keys.ToArray(), 0, result, maxLen, posLen);
			System.Buffer.BlockCopy(map.Values.ToArray(), 0, result, maxLen + posLen, valLen);
			
			string path = Application.persistentDataPath + "/CHUNK_" + dirName;

			if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
			path += "/" + fileName;

			File.WriteAllBytes(path, result);
		}
	}

	public HeatMapObjectContainer container;
	public Vector3 volume = new Vector3(10, 0, 10);
	public Vector2Int resolution = new Vector2Int(256, 256);
	public int pixelRadius = 10;
	public string fileName = "HEATMAP";
	public string directory = "";
	public float updateInterval = 1;
	[Range(10, 100000)]
	public int chunkSize = 500;

	


	Vector3 flattenY = new Vector3(1, 0, 1);
	Vector3 min;
	Vector2Int curPos;
	MapChunk chunk;
	int chunkIndex = 0;

	float time;
	bool saved = false;

	void Start() {
		resolution.x = Mathf.Max(resolution.x, 8);
		resolution.y = Mathf.Max(resolution.y, 8);
		chunk = new MapChunk(chunkSize);
		ClearFiles();
	}

	void LateUpdate() {
		time += Time.deltaTime;
		if (time >= updateInterval) {
			min = Vector3.Scale(transform.position - (volume / 2), flattenY);
			if (container != null) {
				foreach (Transform obj in container.objects) {
					if (obj != null && obj.gameObject.activeInHierarchy && texPos(obj.position, ref curPos))
						chunk[(curPos.y * resolution.x) + curPos.x] += 0.1 * (time / updateInterval);
				}
			}

			if (chunk.length >= chunkSize) {
				chunk.Save(fileName, "chunk" + chunkIndex.ToString() + ".bin");
				chunk.Clear();
				chunkIndex++;
			}
			time = 0;
		}
	}

	void Save() {
		if (saved) return;
		if (chunk.length > 0) chunk.Save(fileName, "chunk" + chunkIndex.ToString() + ".bin");

		string chunkPath = Application.persistentDataPath + "/CHUNK_" + fileName;

		if (!System.IO.Directory.Exists(chunkPath)) return;

		Texture2D tex = new Texture2D(resolution.x, resolution.y);

		Color[] pixels = tex.GetPixels();
		Color mapBase = Color.black;
		for (int i = 0; i < pixels.Length; i++) pixels[i] = mapBase;

		string[] files = System.IO.Directory.GetFiles(chunkPath);
		Vector2Int texPos = new Vector2Int();

		for (int i = 0; i < files.Length; i++) {
			MapChunk savedChunk = new MapChunk(files[i]);
			foreach (int flatPos in savedChunk.map.Keys) {
				texPos.Set(flatPos / resolution.x, flatPos % resolution.x);

				for (int y = System.Math.Max(0, texPos.y - pixelRadius); y < System.Math.Min(resolution.y, texPos.y + pixelRadius); y++) {
					for (int x = System.Math.Max(0, texPos.x - pixelRadius); x < System.Math.Min(resolution.x, texPos.x + pixelRadius); x++) {
						Vector2 curPos = new Vector2(x, y);
						float dist = new Vector2(curPos.x - texPos.x, curPos.y - texPos.y).magnitude / pixelRadius;
						double weight = savedChunk.map[flatPos];

						weight /= savedChunk.max;
						weight *= Mathf.Pow(1 - Mathf.Min(dist, 1), 2);
						weight *= 1f / files.Length;

						pixels[(y * resolution.x) + x].r = Mathf.Min(pixels[(y * resolution.x) + x].r + (float)weight, 0.996f);
					}
				}
			}
		}

		for (int y = 0; y < resolution.y; y++) {
			for (int x = 0; x < resolution.x; x++) {
				float totalWeight = pixels[(y * resolution.x) + x].r;
				float h = Mathf.Lerp(0.725f, 1.16667f, Mathf.Pow(totalWeight, 3)) % 1f;
				float s = Mathf.Lerp(0.997f, 0.6f, totalWeight * totalWeight);
				float v = Mathf.Lerp(0.6f, 0.997f, totalWeight);
				pixels[(y * resolution.x) + x] = Color.HSVToRGB(h, s, v);
			}
		}

		tex.SetPixels(pixels);
		tex.Apply();

		byte[] png = tex.EncodeToPNG();
        Object.Destroy(tex);

		System.IO.File.WriteAllBytes(Application.dataPath + "/" + directory + "/" + fileName + ".png", png);

		foreach (string file in files) {
			System.IO.File.Delete(file);
		}
		saved = true;
	}

	void ClearFiles() {
		string chunkPath = Application.persistentDataPath + "/CHUNK_" + fileName;
		if (System.IO.Directory.Exists(chunkPath)) {
			foreach (string file in System.IO.Directory.GetFiles(chunkPath)) {
				System.IO.File.Delete(file);
			}
		}
	}

	void OnDisable() {
		Save();
	}

	bool texPos(Vector3 pos, ref Vector2Int result) {
		Vector3 offset = pos - min;
		if (offset.x < 0 || offset.x > volume.x || offset.z < 0 || offset.z > volume.z) return false;

		result.x = (int)(offset.z / volume.z * resolution.x);
		result.y = (int)(offset.x / volume.x * resolution.y);

		return true;
	}

	void OnDrawGizmosSelected() {
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(transform.position, volume);	
	}
}
