using System;

namespace Assets.Scripts.Utils {
    public static class MathUtils {
        
        public static bool FuzzyEquals(float a, float b, float epsilon) {
            return Math.Abs(a - b) < epsilon;
        }

    }
}
