using UnityEngine;

namespace Sushil.Systems
{
    // Loads the kΩsmaragd Dark UI sprite sheet from Resources/UI/dark-Buttons.
    // Cached on first access so repeated lookups are free. Safe on WebGL — only
    // uses Resources.LoadAll which works in all build targets.
    public static class DarkButtonSprites
    {
        const string ResourcePath = "UI/dark-Buttons";
        static Sprite[] cachedSprites;
        static bool loadAttempted;

        static void EnsureLoaded()
        {
            if (loadAttempted) return;
            loadAttempted = true;
            cachedSprites = Resources.LoadAll<Sprite>(ResourcePath);
        }

        // Returns a sub-sprite by zero-based index (0 = first slice in the sheet).
        // Falls back to the first available sprite if index is out of range.
        public static Sprite GetSprite(int index)
        {
            EnsureLoaded();
            if (cachedSprites == null || cachedSprites.Length == 0) return null;
            if (index < 0 || index >= cachedSprites.Length) index = 0;
            return cachedSprites[index];
        }

        // Returns a sub-sprite by its name (e.g. "dark-Buttons_3") or null if not found.
        public static Sprite GetByName(string spriteName)
        {
            EnsureLoaded();
            if (cachedSprites == null) return null;
            for (int i = 0; i < cachedSprites.Length; i++)
            {
                if (cachedSprites[i] != null && cachedSprites[i].name == spriteName)
                    return cachedSprites[i];
            }
            return null;
        }

        public static int Count
        {
            get { EnsureLoaded(); return cachedSprites != null ? cachedSprites.Length : 0; }
        }
    }
}
