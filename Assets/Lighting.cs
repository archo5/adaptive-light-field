
using System.Collections.Generic;
using UnityEngine;

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
	public Color color;
	public fixed float vecs[27 * 4];
};

[ExecuteInEditMode]
public class Lighting : MonoBehaviour
{
	public static List<LPoint> lpoints = new List<LPoint>();

	public float stepSize = 1;
	public int extStepCount = 5;

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
				if (len < 1.5f)
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

		// generate colors
		for (int i = 0; i < lpoints.Count; i++)
		{
			var lp = lpoints[i];
			lp.color = new Color(Random.value, Random.value, Random.value);
			lpoints[i] = lp;
		}

		// kill the graphics buffer
		if (gbuf != null)
		{
			gbuf.Dispose();
			gbuf = null;
		}
	}

	void Awake()
	{
		//GeneratePoints();
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.red;
		foreach (var lp in lpoints)
		{
			Gizmos.color = lp.color;
			Gizmos.DrawSphere(lp.pos, 0.05f);
		}
		Gizmos.color = Color.yellow;
		foreach (var lp in lpoints)
		{
			foreach (var vec in lp.vecs)
				Gizmos.DrawLine(lp.pos, lp.pos + vec * 0.45f);
		}
	}

	void OnDestroy()
	{
		if (gbuf != null)
		{
			gbuf.Dispose();
			gbuf = null;
		}
	}

	GraphicsBuffer gbuf;
	//unsafe void OnPreRender()
	unsafe void LateUpdate()
	{
		if (lpoints.Count != 0)
		{
			if (gbuf == null || gbuf.count != lpoints.Count)
			{
				var gblps = new List<GBLPoint>(lpoints.Count);
				foreach (var lp in lpoints)
				{
					var gblp = new GBLPoint()
					{
						pos = lp.pos,
						radius = lp.radius,
						color = lp.color,
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
				gbuf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lpoints.Count, System.Runtime.InteropServices.Marshal.SizeOf<GBLPoint>());
				gbuf.SetData(gblps);
			}
			//Debug.LogFormat("Count:{0}", gbuf.count);
			Shader.SetGlobalBuffer("LightVolPoints", gbuf);
		}
	}
}
