using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Utils {
    public static class VectorUtils {

        public static readonly IEqualityComparer<Vector3> V3_COMPARER = new Vector3CoordComparer();

        private static readonly int AFTER_COMMA = 3; // millimeters...
        private static readonly float DEFAULT_EPSILON = Mathf.Pow(0.1f, AFTER_COMMA);
        private static readonly float DEFAULT_EPSILON_SQR = Mathf.Pow(DEFAULT_EPSILON, 2);
        private static readonly float DEFAULT_ANGLE_EPSILON = 2.0f;

        public static bool FuzzyEquals(Vector3 a, Vector3 b, float epsilon) {
            return MathUtils.FuzzyEquals(Vector3.SqrMagnitude(a - b), 0f, epsilon);
        }

        public static bool FuzzyEquals(Vector3 a, Vector3 b) {
            return FuzzyEquals(a, b, DEFAULT_EPSILON_SQR);
        }

        public static bool IsOrientationEqual(Vector3 a, Vector3 b, float epsilon) {
            //return Vector3.Angle(a, b) < epsilon;
            return FuzzyEquals(a.normalized, b.normalized, epsilon);
        }

        public static bool IsOrientationEqual(Vector3 a, Vector3 b) {
            //return IsOrientationEqual(a, b, DEFAULT_ANGLE_EPSILON);
            return FuzzyEquals(a.normalized, b.normalized, DEFAULT_EPSILON_SQR);
        }

        private class Vector3CoordComparer : IEqualityComparer<Vector3> {
            public bool Equals(Vector3 a, Vector3 b) {
                //if (Mathf.Abs(a.x - b.x) > 0.1) return false;
                //if (Mathf.Abs(a.y - b.y) > 0.1) return false;
                //if (Mathf.Abs(a.z - b.z) > 0.1) return false;

                //return true; //indeed, very close
                return FuzzyEquals(a, b, DEFAULT_EPSILON);
            }

            public int GetHashCode(Vector3 obj) {
                //a cruder than default comparison, allows to compare very close-vector3's into same hash-code.
                //return Math.Round(obj.x, 1).GetHashCode()
                //     ^ Math.Round(obj.y, 1).GetHashCode() << 2
                //     ^ Math.Round(obj.z, 1).GetHashCode() >> 2;
                return Math.Round(Vector3.SqrMagnitude(obj), AFTER_COMMA).GetHashCode();
            }
        }

    }
}