
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct LPoint
{
	public Vector3 pos;
	public float dist;
	public Color color;
	public List<Vector3> vecs;
	public float radius;
};

public unsafe struct GBLPoint
{
	public Vector3 pos;
	public float radius;
	public Vector3 color;
	public int vecCount;
	public fixed float vecs[27 * 4];
};

[System.Serializable]
public struct AABB3f
{
	public float x0, y0, z0;
	public float x1, y1, z1;

	public AABB3f GetCell(int x, int y, int z, Vector3Int size)
	{
		return new AABB3f()
		{
			x0 = Mathf.LerpUnclamped(x0, x1, x / (float) size.x),
			y0 = Mathf.LerpUnclamped(y0, y1, y / (float) size.y),
			z0 = Mathf.LerpUnclamped(z0, z1, z / (float) size.z),
			x1 = Mathf.LerpUnclamped(x0, x1, (x + 1) / (float) size.x),
			y1 = Mathf.LerpUnclamped(y0, y1, (y + 1) / (float) size.y),
			z1 = Mathf.LerpUnclamped(z0, z1, (z + 1) / (float) size.z),
		};
	}
	public Vector3 LimitPoint(Vector3 p)
	{
		if (p.x < x0) p.x = x0; else if (p.x > x1) p.x = x1;
		if (p.y < y0) p.y = y0; else if (p.y > y1) p.y = y1;
		if (p.z < z0) p.z = z0; else if (p.z > z1) p.z = z1;
		return p;
	}
	public bool IntersectsSphere(Vector3 pos, float radius)
	{
		Vector3 cp = LimitPoint(pos);
		return (pos - cp).magnitude < radius;
	}
};

[ExecuteInEditMode]
public class Lighting : MonoBehaviour
{
	// point generation
	[HideInInspector]
	public List<LPoint> lpoints = new List<LPoint>();
	public float stepSize = 1;
	public int extStepCount = 5;

	// point acceleration
	public AABB3f gridAABB = new AABB3f() { x0 = -5, y0 = -1, z0 = -5, x1 = 5, y1 = 3, z1 = 5 };
	public float gridCellSize = 1;
	[HideInInspector]
	public Vector3Int gridSize;
	// the grid referring to point lists below
	// encoding: 20b offset, 12b count
	[HideInInspector]
	public int[] gridListOffsets = new int[0];
	// the point lists of the grid, indexed by the grid above
	[HideInInspector]
	public List<int> gridPointLists = new List<int>();

	// light baking
	public Color ambientColor = new Color(0.1f, 0.2f, 0.4f);

	// debug drawing
	public bool drawSpheres = true;
	public bool drawVectors = true;

	void BuildGrid()
	{
		gridSize = new Vector3Int()
		{
			x = Mathf.CeilToInt((gridAABB.x1 - gridAABB.x0) / gridCellSize),
			y = Mathf.CeilToInt((gridAABB.y1 - gridAABB.y0) / gridCellSize),
			z = Mathf.CeilToInt((gridAABB.z1 - gridAABB.z0) / gridCellSize),
		};

		gridListOffsets = new int[gridSize.x * gridSize.y * gridSize.z];
		gridPointLists.Clear();

		int maxCountPerCell = 0;
		for (int z = 0; z < gridSize.z; z++)
		{
			for (int y = 0; y < gridSize.y; y++)
			{
				for (int x = 0; x < gridSize.x; x++)
				{
					AABB3f cell = gridAABB.GetCell(x, y, z, gridSize);
					int offset = gridPointLists.Count;
					for (int i = 0; i < lpoints.Count; i++)
					{
						var lp = lpoints[i];
						if (cell.IntersectsSphere(lp.pos, lp.radius))
						{
							gridPointLists.Add(i);
						}
					}
					int count = gridPointLists.Count - offset;
					gridListOffsets[x + y * gridSize.x + z * gridSize.x * gridSize.y] = offset | (count << 20);
					maxCountPerCell = Mathf.Max(maxCountPerCell, count);
				}
			}
		}
		Debug.LogFormat("Built grid: {0}x{1}x{2}, max # per cell: {3}, avg # per cell: {4}",
			gridSize.x, gridSize.y, gridSize.z,
			maxCountPerCell,
			gridPointLists.Count / (gridSize.x * gridSize.y * gridSize.z));
	}

	[ContextMenu("Generate points")]
	void GeneratePoints()
	{
		lpoints.Clear();

#if false
		var mrs = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
		foreach (var mr in mrs)
		{
			var mf = mr.GetComponent<MeshFilter>();
			var xf = mr.transform;
			var mesh = mf.sharedMesh;

			var posns = mesh.vertices;
			foreach (var p in posns)
				lpoints.Add(new LPoint() { pos = xf.TransformPoint(p) });
			var tris = mesh.triangles;
			for (int i = 0; i + 2 < tris.Length; i += 3)
			{
				int i0 = tris[i];
				int i1 = tris[i + 1];
				int i2 = tris[i + 2];
				Vector3 p0 = xf.TransformPoint(posns[i0]);
				Vector3 p1 = xf.TransformPoint(posns[i1]);
				Vector3 p2 = xf.TransformPoint(posns[i2]);
				float el01 = (p1 - p0).magnitude;
				float el12 = (p2 - p1).magnitude;
				float el20 = (p0 - p2).magnitude;
				int sub01 = Mathf.RoundToInt(el01);
				int sub12 = Mathf.RoundToInt(el12);
				int sub20 = Mathf.RoundToInt(el20);
				// generate edge points
				for (int j = 1; j < sub01; j++)
				{
					lpoints.Add(new LPoint() { pos = Vector3.LerpUnclamped(p0, p1, j / (float) sub01) });
				}
				for (int j = 1; j < sub12; j++)
				{
					lpoints.Add(new LPoint() { pos = Vector3.LerpUnclamped(p1, p2, j / (float) sub12) });
				}
				for (int j = 1; j < sub20; j++)
				{
					lpoints.Add(new LPoint() { pos = Vector3.LerpUnclamped(p2, p0, j / (float) sub20) });
				}
				// generate inside barycentrics
				for (int j = 1; j < sub01; j++)
				{
					float jq = j / (float) sub01;
					for (int k = 1; k < sub12; k++)
					{
						float kq = k / (float) sub12;
						if (jq + kq < 1)
						{
							lpoints.Add(new LPoint() { pos = p1 + (p0 - p1) * jq + (p2 - p1) * kq });
						}
					}
				}
			}
		}
#endif

		var lsds = FindObjectsByType<LitSurfDesc>(FindObjectsSortMode.None);
		foreach (var lsd in lsds)
		{
			var xf = lsd.transform;
			foreach (var q in lsd.quads)
			{
				LSDQuad xq = q.Transformed(xf);
				float lenh = (xq.p10 + xq.p11 - xq.p00 - xq.p01).magnitude * 0.5f;
				float lenv = (xq.p01 + xq.p11 - xq.p00 - xq.p10).magnitude * 0.5f;
				int subh = Mathf.CeilToInt(lenh / stepSize);
				int subv = Mathf.CeilToInt(lenv / stepSize);
				for (int y = -extStepCount; y <= subv + extStepCount; y++)
				{
					int yc = Mathf.Clamp(y, 0, subv);
					float ycq = yc / (float) subv;
					int yext = y - yc;
					for (int x = -extStepCount; x < subh + extStepCount; x++)
					{
						int xc = Mathf.Clamp(x, 0, subh);
						float xcq = xc / (float) subh;
						int xext = x - xc;

						Vector3 basePos = Vector3.LerpUnclamped(
							Vector3.LerpUnclamped(xq.p00, xq.p10, xcq),
							Vector3.LerpUnclamped(xq.p01, xq.p11, xcq), ycq);
						Vector3 dirX = Vector3.LerpUnclamped(xq.p10 - xq.p00, xq.p11 - xq.p01, ycq).normalized;
						Vector3 dirY = Vector3.LerpUnclamped(xq.p01 - xq.p00, xq.p11 - xq.p10, xcq).normalized;
						Vector3 dirZ = Vector3.Cross(dirX, dirY).normalized;

						for (int zext = 0; zext <= extStepCount; zext++)
						{
							Vector3 finalPos = basePos + (dirX * xext + dirY * yext + dirZ * zext) * stepSize;
							float dist = (basePos - finalPos).magnitude;
							lpoints.Add(new LPoint() { pos = finalPos, dist = dist });
						}
					}
				}
			}
		}

		// dedup (keep closest to surface)
		for (int i = 0; i < lpoints.Count; i++)
		{
			for (int j = i + 1; j < lpoints.Count; j++)
			{
				if ((lpoints[i].pos - lpoints[j].pos).magnitude < stepSize * 0.4999f)
				{
					if (lpoints[i].dist < lpoints[j].dist)
					{
						lpoints.RemoveAt(j--);
					}
					else
					{
						lpoints.RemoveAt(i--);
						break;
					}
				}
			}
		}

		// find nearby points
		int largestNearbyCount = 0;
		for (int i = 0; i < lpoints.Count; i++)
		{
			var lp = lpoints[i];
			lp.vecs = new List<Vector3>();
			float radius = 0;
			for (int j = 0; j < lpoints.Count; j++)
			{
				if (i == j)
					continue;
				Vector3 vec = lpoints[j].pos - lp.pos;
				float len = vec.magnitude;
				if (len < stepSize * 1.1f)
				{
					radius = Mathf.Max(radius, len);
					lp.vecs.Add(vec);
				}
			}
			lp.radius = radius;
			lpoints[i] = lp;
			largestNearbyCount = Mathf.Max(largestNearbyCount, lp.vecs.Count);
		}
		Debug.LogFormat("Points generated: {0}", lpoints.Count);
		Debug.LogFormat("Largest nearby count: {0}", largestNearbyCount);

		BuildGrid();

		// generate colors
		for (int i = 0; i < lpoints.Count; i++)
		{
			var lp = lpoints[i];
			lp.color = new Color(Random.value, Random.value, Random.value);
			lpoints[i] = lp;
		}

		DeleteGraphicsBuffers();
	}

	[ContextMenu("Bake lighting")]
	void BakeLighting()
	{
		var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
		Physics.queriesHitBackfaces = true;
		for (int i = 0; i < lpoints.Count; i++)
		{
			var lp = lpoints[i];
			lp.color = ambientColor;
			foreach (var L in lights)
			{
				if (L.type == LightType.Directional)
				{
					Vector3 dir = L.transform.forward;
					if (!Physics.Raycast(lp.pos - dir * 1000.001f, dir, 1000))
					{
						lp.color += L.color;
					}
				}
				else if (L.type == LightType.Rectangle)
				{
					Vector3 dir = L.transform.forward;
					Vector3 lightPos = L.transform.position;
					Vector3 l2p = lp.pos - L.transform.position;
					Vector3 l2pd = l2p.normalized;
					float dist = l2p.magnitude;
					if (!Physics.Raycast(lightPos + l2pd * 0.001f, l2pd, dist - 0.002f))
					{
						float amount = Mathf.Clamp01(1 - dist / L.range) * Mathf.Clamp01(Vector3.Dot(dir, l2pd));
						lp.color += L.color * amount;
					}
				}
			}
			lpoints[i] = lp;
		}
		DeleteGraphicsBuffers();
	}

	void DeleteGraphicsBuffers()
	{
		if (gbufPoints != null)
		{
			gbufPoints.Dispose();
			gbufPoints = null;
		}
		if (gbufGrid != null)
		{
			gbufGrid.Dispose();
			gbufGrid = null;
		}
		if (gbufPtLists != null)
		{
			gbufPtLists.Dispose();
			gbufPtLists = null;
		}
	}

	void Awake()
	{
		//GeneratePoints();
	}

	void OnDrawGizmos()
	{
		Gizmos.DrawWireCube(new Vector3()
		{
			x = (gridAABB.x0 + gridAABB.x1) / 2,
			y = (gridAABB.y0 + gridAABB.y1) / 2,
			z = (gridAABB.z0 + gridAABB.z1) / 2,
		},
		new Vector3()
		{
			x = gridAABB.x1 - gridAABB.x0,
			y = gridAABB.y1 - gridAABB.y0,
			z = gridAABB.z1 - gridAABB.z0,
		});
		if (drawSpheres)
		{
			Gizmos.color = Color.red;
			foreach (var lp in lpoints)
			{
				Gizmos.color = lp.color;
				Gizmos.DrawSphere(lp.pos, 0.05f);
			}
		}
		if (drawVectors)
		{
			Gizmos.color = Color.yellow;
			foreach (var lp in lpoints)
			{
				foreach (var vec in lp.vecs)
					Gizmos.DrawLine(lp.pos, lp.pos + vec * 0.45f);
			}
		}
	}

	void OnDestroy()
	{
		DeleteGraphicsBuffers();
	}

	GraphicsBuffer gbufPoints;
	GraphicsBuffer gbufGrid;
	GraphicsBuffer gbufPtLists;
	//unsafe void OnPreRender()
	unsafe void LateUpdate()
	{
		if (lpoints.Count != 0)
		{
			if (gbufPoints == null || gbufPoints.count != lpoints.Count)
			{
				if (gbufPoints != null)
					gbufPoints.Dispose();
				var gblps = new List<GBLPoint>(lpoints.Count);
				foreach (var lp in lpoints)
				{
					var gblp = new GBLPoint()
					{
						pos = lp.pos,
						radius = lp.radius,
						color = new Vector3() { x = lp.color.r, y = lp.color.g, z = lp.color.b },
						vecCount = Mathf.Min(lp.vecs.Count, 27)
					};
					for (int i = 0; i < lp.vecs.Count && i < 27; i++)
					{
						var nv = lp.vecs[i].normalized;
						float len = lp.vecs[i].magnitude;
						gblp.vecs[i * 4 + 0] = nv.x;
						gblp.vecs[i * 4 + 1] = nv.y;
						gblp.vecs[i * 4 + 2] = nv.z;
						gblp.vecs[i * 4 + 3] = len;
					}
					gblps.Add(gblp);
				}
				gbufPoints = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lpoints.Count, System.Runtime.InteropServices.Marshal.SizeOf<GBLPoint>());
				gbufPoints.SetData(gblps);
			}
			//Debug.LogFormat("Count:{0}", gbuf.count);
			Shader.SetGlobalBuffer("LightVolPoints", gbufPoints);
		}

		if (gridSize != Vector3Int.zero)
		{
			int gridCellCount = gridSize.x * gridSize.y * gridSize.z;
			if (gbufGrid == null || gbufGrid.count != gridCellCount)
			{
				if (gbufGrid != null)
					gbufGrid.Dispose();
				gbufGrid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridCellCount, 4);
			}
			gbufGrid.SetData(gridListOffsets);
			Shader.SetGlobalBuffer("LightVolGrid", gbufGrid);

			if (gbufPtLists == null || gbufPtLists.count != gridPointLists.Count)
			{
				if (gbufPtLists != null)
					gbufPtLists.Dispose();
				gbufPtLists = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridPointLists.Count, 4);
			}
			gbufPtLists.SetData(gridPointLists);
			Shader.SetGlobalBuffer("LightVolPtLists", gbufPtLists);

			Shader.SetGlobalVector("LightVolMin", new Vector4() { x = gridAABB.x0, y = gridAABB.y0, z = gridAABB.z0 });
			Shader.SetGlobalVector("LightVolMax", new Vector4() { x = gridAABB.x1, y = gridAABB.y1, z = gridAABB.z1 });
			Shader.SetGlobalInteger("LightVolGridSizeX", gridSize.x);
			Shader.SetGlobalInteger("LightVolGridSizeY", gridSize.y);
			Shader.SetGlobalInteger("LightVolGridSizeZ", gridSize.z);
		}
	}
}
