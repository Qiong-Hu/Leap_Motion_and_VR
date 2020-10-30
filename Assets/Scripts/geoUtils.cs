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


        #endregion
    }
}

