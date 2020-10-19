using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FARVR.GeoParams {
    public class Geometry : MonoBehaviour {
		// Define params of plane
		public struct PlaneParams {
			public string name;
			public int index;
			public Vector3 position;
			public Vector3 forwardDir;
			public Vector3 normalDir;
			public bool isEmpty;
			public float confidence; // The smaller the better
		}

		// Define params of line
		public struct LineParams {
			public string name;
			public int index;
			public Vector3 position;
			public Vector3 direction;
			public bool isEmpty;
			public float confidence; // The smaller the better
		}

		// Define params of point
		public struct PointParams {
			public string name;
			public int index;
			public Vector3 position;
			public bool isEmpty;
			public float confidence; // The smaller the better
		}
		
	}
}

