
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct LSDQuad
{
	public Vector3 p00;
	public Vector3 p10;
	public Vector3 p01;
	public Vector3 p11;

	public LSDQuad Transformed(Transform xf)
	{
		return new LSDQuad()
		{
			p00 = xf.TransformPoint(p00),
			p10 = xf.TransformPoint(p10),
			p01 = xf.TransformPoint(p01),
			p11 = xf.TransformPoint(p11),
		};
	}
};

public class LitSurfDesc : MonoBehaviour
{
	public List<LSDQuad> quads = new List<LSDQuad>();

	static Vector3[] tmp = new Vector3[4];
	void OnDrawGizmos()
	{
		var xf = transform;
		foreach (var q in quads)
		{
			var xq = q.Transformed(xf);
			tmp[0] = xq.p00;
			tmp[1] = xq.p10;
			tmp[2] = xq.p11;
			tmp[3] = xq.p01;
			Gizmos.color = Color.blue;
			Gizmos.DrawLineStrip(tmp, true);
			Vector3 c = (xq.p00 + xq.p01 + xq.p10 + xq.p11) * 0.25f;
			Vector3 dirX = (xq.p10 + xq.p11 - xq.p00 - xq.p01).normalized;
			Vector3 dirY = (xq.p01 + xq.p11 - xq.p00 - xq.p10).normalized;
			Vector3 dirZ = Vector3.Cross(dirX, dirY).normalized;
			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(c, c + dirZ);
		}
	}
}
