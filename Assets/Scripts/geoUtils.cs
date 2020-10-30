using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FARVR.MathUtils;
using FARVR.GeoParams;

namespace FARVR.GeoUtils {
    public class geoUtils {
        const float planeDirPosRatio = 0.8f;
        const float lineDirPosRatio = 0.8f;

        const float singlePairRatio = 0.8f;
        const float pairDirPosRatio = 0.8f;

        #region Single geo comparison
        // Calc similarity of 2 planes
        public static float PlaneSimilarity(Geometry.PlaneParams planeEva, Geometry.PlaneParams planeTar, float refDistance, float planeDirPosRatio = planeDirPosRatio) {
            return mathUtils.DirectionSimilarity(planeEva.normalDir, planeTar.normalDir) * planeDirPosRatio
                + Vector3.Distance(planeEva.position, planeTar.position) / refDistance * (1 - planeDirPosRatio);
        }

        // Calc similarity between 2 lines
        public static float LineSimilarity(Geometry.LineParams lineEva, Geometry.LineParams lineTar, float refDistance, float lineDirPosRatio = lineDirPosRatio) {
            if (mathUtils.VectorNeedFlip(lineEva.direction, lineTar.direction)) {
                lineEva.direction = mathUtils.VectorFlip(lineEva.direction);
            }
            return mathUtils.DirectionSimilarity(lineEva.direction, lineTar.direction) * lineDirPosRatio
                + Vector3.Distance(lineEva.position, lineTar.position) / refDistance * (1 - lineDirPosRatio);
        }

        // Calc similarity between 2 points
        public static float PointSimilarity(Geometry.PointParams pointEva, Geometry.PointParams pointTar, float refDistance) {
            return Vector3.Distance(pointEva.position, pointTar.position) / refDistance;
        }

        #endregion

        #region Geo pair comparison
        // The smaller the score is, the more similar the evaluated pair are to the target pair
        // For plane pair
        public static float PlanePairSimilarity(Geometry.PlaneParams eva1, Geometry.PlaneParams eva2, Geometry.PlaneParams tar1, Geometry.PlaneParams tar2, float singlePairRatio = singlePairRatio, float pairDirPosRatio = pairDirPosRatio) {
            float score = 0f;

            // If eva1 == eva2 (same plane)
            if (eva1.position == eva2.position && eva1.normalDir == eva2.normalDir) {
                score = Mathf.Infinity;
            }
            else {
                score += (1 - pairDirPosRatio) * mathUtils.VectorSimilarity(eva1.position - eva2.position, tar1.position - tar2.position); // Similarity of two relative vectors between two plane pairs
                score += pairDirPosRatio * mathUtils.DirectionSimilarity(Quaternion.FromToRotation(eva1.normalDir, tar1.normalDir) * eva2.normalDir, tar2.normalDir); // Rotate eva pair so eva[0] align with tar[0], compare rotated eva[1] with tar[1]
                score = score * (1 - singlePairRatio) + (eva1.confidence + eva2.confidence) / 2 * singlePairRatio;
            }

            return score;
        }

        // For line pair

        // For point pair

        // For plane-line pair

        // For plane-point pair

        // For line-point pair


        #endregion
    }
}

