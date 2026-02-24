using UnityEngine;

namespace Sushil.Systems
{
    public static class OverlayTypography
    {
        static Font cachedFallback;
        static Font cachedDynamic;

        public static Font GetFont(int size)
        {
            if (cachedFallback == null)
                cachedFallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

#if UNITY_WEBGL
            // WebGL cannot rely on OS fonts; stick to a deterministic bundled font.
            return cachedFallback;
#else
            if (cachedDynamic == null)
            {
                string[] spooky = { "Chiller", "Creepster", "Nosifer", "Papyrus", "Impact", "Arial Black" };
                cachedDynamic = Font.CreateDynamicFontFromOSFont(spooky, Mathf.Clamp(size, 16, 72));
            }

            return cachedDynamic != null ? cachedDynamic : cachedFallback;
#endif
        }
    }
}
