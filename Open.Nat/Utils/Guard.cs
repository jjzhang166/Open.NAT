using System;

namespace Open.Nat
{
    internal class Guard
    {
        public static void IsInRange(int paramValue, int lowerBound, int upperBound, string paramName)
        {
            if (paramValue < lowerBound || paramValue > upperBound)
                throw new ArgumentOutOfRangeException(paramName);
        }

        public static void IsTrue(bool exp, string paramName)
        {
            if(!exp)
                throw new ArgumentOutOfRangeException(paramName);
        }
    }
}