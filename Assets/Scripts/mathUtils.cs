using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FARVR.MathUtils {
    public class mathUtils {
		/// <summary>
		/// Compare curr ArrayList to ref ArrayList
		/// </summary>
		/// <param name="list1"> current ArrayList </param>
		/// <param name="list2"> reference ArrayList </param>
		private static bool CompareArrayList(ArrayList list1, ArrayList list2) {
			if (list1.Count != list2.Count) {
				return false;
			}
			else {
				for (int i = 0; i < list1.Count; i++) {
					if (Object.Equals(list2[i], null)) {
						continue;
					}
					if (!Object.Equals(list1[i], list2[i])) {
						return false;
					}
				}
				return true;
			}
		}

		/// <summary>
		/// Compare curr Dict to ref Dict
		/// </summary>
		/// <param name="dict1"> current gesture param dict </param>
		/// <param name="dict2"> reference gesture param dict </param>
		public static bool CompareDict(Dictionary<string, ArrayList> dict1, Dictionary<string, ArrayList> dict2) {
			bool flag = true;
			foreach (string key in dict2.Keys) {
				if (!CompareArrayList(dict1[key], dict2[key])) {
					flag = false;
					break;
				}
			}
			return flag;
		}

		/// <summary>
		/// Simple permutation of int list {0~n} in the order that their sum from small to large
		/// </summary>
		/// eg: num=5, return {(0,0),(0,1),(1,0),(0,2),(1,1),(2,0),(0,3),(1,2),...}
		public static List<Vector2Int> Permutation(int n) {
            List<Vector2Int> results = new List<Vector2Int>();

            for (int currSum = 0; currSum <= 2 * n; currSum++) {
                for (int i = 0; i < n; i++) {
                    for (int j = 0; j < n; j++) {
                        if (i + j == currSum) {
                            results.Add(new Vector2Int(i, j));
                        }
                    }
                }
            }

            return results;
        }
    
		/// <summary>
        /// x = 1: return 0; x = 0: return infinity; x large: return large
        /// The closer x is to 1, the smaller the return result is
        /// </summary>
		public static float CloseTo1(float x) {
			float result = Mathf.Abs(Mathf.Log(x));
			return result;
        }

		public static bool VectorNeedFlip(Vector3 vecEva, Vector3 vecTar) {
			if (Mathf.Abs(Vector3.Angle(vecEva, vecTar)) > 90f) {
				return true;
			}
			else {
				return false;
            }
		}

		public static Vector3 VectorFlip(Vector3 vecEva) {
			return Vector3.zero - vecEva;
		}

		/// <summary>
        /// The smaller the result is, the more similar the two vectors are
        /// </summary>
		public static float VectorSimilarity(Vector3 vecEva, Vector3 vecTar, float dirDisRatio = 0.8f) {
			if (vecEva == Vector3.zero) {
				return Mathf.Infinity;
            }

			if (VectorNeedFlip(vecEva, vecTar)) {
				vecEva = VectorFlip(vecEva);
            }

			return DirectionSimilarity(vecEva.normalized, vecTar.normalized) * dirDisRatio 
				+ Vector3.Distance(vecEva, vecTar) / vecTar.magnitude * (1 - dirDisRatio); // For pseudo- normalization
		}

		/// <summary>
        /// result = 1: two quaternions are same; = 0: opposite
        /// </summary>
        /// <returns></returns>
		private static float QuaternionSimilarity(Quaternion qua1, Quaternion qua2) {
			return qua1.x * qua2.x + qua1.y * qua2.y + qua1.z * qua2.z + qua1.w * qua2.w;
        }

		/// <summary>
        /// result = 0: two directions are same; = 1: opposite
        /// </summary>
        /// <returns></returns>
		public static float DirectionSimilarity(Vector3 vec1, Vector3 vec2) {
			return Mathf.Abs(Vector3.Angle(vec1, vec2)) / 180f;
		}
	}
}
