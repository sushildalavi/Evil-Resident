using UnityEngine;

namespace Sushil.Systems
{
    public static class OverlayTypography
    {
        static Font cachedFallback;

        public static Font GetFont(int size)
        {
            if (cachedFallback == null)
                cachedFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Always use deterministic built-in font for consistent look across Editor/WebGL/Mobile.
            return cachedFallback;
        }
    }
}
