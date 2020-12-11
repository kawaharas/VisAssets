using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VIS
{
	public class LineRenderSettings : MonoBehaviour
	{
		[SerializeField]
		LineRenderer rend;
		Vector3[] points;
		public LayerMask layerMask;

		void Start()
		{
			rend = gameObject.GetComponent<LineRenderer>();
			points = new Vector3[2];
			//			points[0] = Vector3.zero;
			points[0] = new Vector3(0, 0, -20);
			points[1] = transform.position + new Vector3(0, 0, 20);
			rend.SetPositions(points);
			rend.enabled = true;
		}

		void Update()
		{
			AlignLineRenderer(rend);
		}

		public void AlignLineRenderer(LineRenderer rend)
		{
			Ray ray;
			ray = new Ray(transform.position, transform.forward * 10);
			RaycastHit hit;

			if (Physics.Raycast(ray, out hit, layerMask))
			{
				points[1] = transform.position + new Vector3(0, 0, hit.distance);
			}
			else
			{
				points[1] = transform.position + new Vector3(0, 0, 20);
			}

			rend.SetPositions(points);
		}
	}
}