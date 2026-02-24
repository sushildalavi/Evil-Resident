using UnityEngine;

namespace Sushil.Systems
{
    public static class OverlayTypography
    {
        public static Font GetFont(int size)
        {
            string[] spooky = { "Chiller", "Creepster", "Nosifer", "Papyrus", "Impact", "Arial Black" };
            Font dynamic = Font.CreateDynamicFontFromOSFont(spooky, size);
            if (dynamic != null) return dynamic;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
