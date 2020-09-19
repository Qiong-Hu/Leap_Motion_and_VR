﻿using System.Collections;
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
        /// The smaller the result is, the closer x is to 1
        /// </summary>
		public static float CloseTo1(float x) {
			float result = Mathf.Abs(Mathf.Log(x));
			return result;
        }

		/// <summary>
        /// The smaller the result is, the more similar the two vectors are
        /// </summary>
		public static float VectorSimilarity(Vector3 vecEva, Vector3 vecTarget) {
			float dirDisRatio = 0.8f;

			float score;
			if (vecEva == Vector3.zero) {
				score = Mathf.Infinity;
				return score;
            }

			if (Mathf.Abs(Vector3.Angle(vecEva, vecTarget)) > 90f) {
				vecEva = Vector3.zero - vecEva;
			}

			score = Mathf.Abs(Vector3.Angle(vecEva, vecTarget)) / 90f * dirDisRatio
				+ Vector3.Distance(vecEva, vecTarget) / vecTarget.magnitude * (1 - dirDisRatio);

			return score;
		}
	}
}
