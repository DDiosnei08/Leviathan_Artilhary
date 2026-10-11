using System;

namespace Leviathan.Terrain
{
    /// <summary>
    /// Convenção ÚNICA de gradiente e inclinação do terreno.
    ///
    /// O gradiente de uma função de altura h(x, z) é o par (dh/dx, dh/dz). Ele é adimensional
    /// (metros de altura por metro horizontal). A inclinação em graus é:
    ///     theta = atan(|gradiente|) * 180 / pi
    /// Máscaras, estatísticas e depuração devem usar SOMENTE estas funções, para que o
    /// mesmo valor nunca seja interpretado como gradiente em um lugar e como ângulo em outro.
    /// </summary>
    public static class SlopeMath
    {
        const double RadiansToDegrees = 180.0 / Math.PI;
        const double DegreesToRadians = Math.PI / 180.0;

        public static float GradientToDegrees(float dhdx, float dhdz)
        {
            double magnitude = Math.Sqrt((double)dhdx * dhdx + (double)dhdz * dhdz);
            return (float)(Math.Atan(magnitude) * RadiansToDegrees);
        }

        /// <summary>Magnitude de gradiente equivalente a um ângulo (0 a menos de 90 graus).</summary>
        public static float DegreesToGradient(float degrees)
        {
            return (float)Math.Tan(degrees * DegreesToRadians);
        }
    }
}
