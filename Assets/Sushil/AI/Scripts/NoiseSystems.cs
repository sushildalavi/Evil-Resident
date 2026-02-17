using System;
using UnityEngine;

namespace Sushil.Systems
{
    public static class NoiseSystem
    {
        public static event Action<Vector3, float, string> OnNoise;

        public static void Emit(Vector3 position, float intensity, string type = "generic")
        {
            OnNoise?.Invoke(position, intensity, type);
        }
        
        public static void EmitNoise(Vector3 pos, float intensity, string type = "noise")
        {
            OnNoise?.Invoke(pos, intensity, type);
        }
    }
}