mergeInto(LibraryManager.library, {
  ConfigureWebGLPageForNoZoom: function () {
    try {
      function ensureFullscreenButton() {
        function requestFs() {
          var canvas = Module && Module['canvas'] ? Module['canvas'] : null;
          var target = (canvas && canvas.parentElement) ? canvas.parentElement : (canvas || document.documentElement);
          if (target.requestFullscreen) target.requestFullscreen();
          else if (target.webkitRequestFullscreen) target.webkitRequestFullscreen();
          else if (target.msRequestFullscreen) target.msRequestFullscreen();
        }

        var unityBtn = document.getElementById('unity-fullscreen-button');
        if (unityBtn) {
          if (unityBtn.parentElement !== document.body) {
            document.body.appendChild(unityBtn);
          }
          unityBtn.style.display = 'block';
          unityBtn.style.visibility = 'visible';
          unityBtn.style.opacity = '1';
          unityBtn.style.pointerEvents = 'auto';
          unityBtn.style.position = 'fixed';
          unityBtn.style.left = 'auto';
          unityBtn.style.top = 'auto';
          unityBtn.style.right = 'max(16px, env(safe-area-inset-right))';
          unityBtn.style.bottom = 'max(16px, env(safe-area-inset-bottom))';
          unityBtn.style.transform = 'none';
          unityBtn.style.zIndex = '9999';
          return;
        }

        var fallbackId = 'webgl-fixed-fullscreen-button';
        var fallback = document.getElementById(fallbackId);
        if (!fallback) {
          fallback = document.createElement('button');
          fallback.id = fallbackId;
          fallback.textContent = 'Fullscreen';
          fallback.type = 'button';
          fallback.onclick = requestFs;
          document.body.appendChild(fallback);
        }

        fallback.style.position = 'fixed';
        fallback.style.left = 'auto';
        fallback.style.top = 'auto';
        fallback.style.right = 'max(16px, env(safe-area-inset-right))';
        fallback.style.bottom = 'max(16px, env(safe-area-inset-bottom))';
        fallback.style.zIndex = '9999';
        fallback.style.padding = '10px 14px';
        fallback.style.border = '1px solid rgba(255,255,255,0.35)';
        fallback.style.borderRadius = '8px';
        fallback.style.background = 'rgba(0,0,0,0.55)';
        fallback.style.color = '#fff';
        fallback.style.font = '600 13px system-ui, -apple-system, Segoe UI, Roboto, sans-serif';
        fallback.style.cursor = 'pointer';
      }

      ensureFullscreenButton();
      // Force a stable mobile/desktop viewport so browsers do not auto-zoom the page.
      var meta = document.querySelector('meta[name="viewport"]');
      if (!meta) {
        meta = document.createElement('meta');
        meta.name = 'viewport';
        document.head.appendChild(meta);
      }

      meta.content = 'width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover';

      document.documentElement.style.overflow = 'hidden';
      document.body.style.overflow = 'hidden';
      document.body.style.margin = '0';
      document.body.style.padding = '0';
      document.body.style.backgroundColor = '#000';
      document.body.style.touchAction = 'none';
      ensureFullscreenButton();
    } catch (e) {
      console.warn('ConfigureWebGLPageForNoZoom failed:', e);
    }
  },

  SetWebGLFixedResolution: function (width, height) {
    try {
      function ensureFullscreenButton() {
        function requestFs() {
          var canvas = Module && Module['canvas'] ? Module['canvas'] : null;
          var target = (canvas && canvas.parentElement) ? canvas.parentElement : (canvas || document.documentElement);
          if (target.requestFullscreen) target.requestFullscreen();
          else if (target.webkitRequestFullscreen) target.webkitRequestFullscreen();
          else if (target.msRequestFullscreen) target.msRequestFullscreen();
        }

        var unityBtn = document.getElementById('unity-fullscreen-button');
        if (unityBtn) {
          if (unityBtn.parentElement !== document.body) {
            document.body.appendChild(unityBtn);
          }
          unityBtn.style.display = 'block';
          unityBtn.style.visibility = 'visible';
          unityBtn.style.opacity = '1';
          unityBtn.style.pointerEvents = 'auto';
          unityBtn.style.position = 'fixed';
          unityBtn.style.left = 'auto';
          unityBtn.style.top = 'auto';
          unityBtn.style.right = 'max(16px, env(safe-area-inset-right))';
          unityBtn.style.bottom = 'max(16px, env(safe-area-inset-bottom))';
          unityBtn.style.transform = 'none';
          unityBtn.style.zIndex = '9999';
          return;
        }

        var fallbackId = 'webgl-fixed-fullscreen-button';
        var fallback = document.getElementById(fallbackId);
        if (!fallback) {
          fallback = document.createElement('button');
          fallback.id = fallbackId;
          fallback.textContent = 'Fullscreen';
          fallback.type = 'button';
          fallback.onclick = requestFs;
          document.body.appendChild(fallback);
        }

        fallback.style.position = 'fixed';
        fallback.style.left = 'auto';
        fallback.style.top = 'auto';
        fallback.style.right = 'max(16px, env(safe-area-inset-right))';
        fallback.style.bottom = 'max(16px, env(safe-area-inset-bottom))';
        fallback.style.zIndex = '9999';
        fallback.style.padding = '10px 14px';
        fallback.style.border = '1px solid rgba(255,255,255,0.35)';
        fallback.style.borderRadius = '8px';
        fallback.style.background = 'rgba(0,0,0,0.55)';
        fallback.style.color = '#fff';
        fallback.style.font = '600 13px system-ui, -apple-system, Segoe UI, Roboto, sans-serif';
        fallback.style.cursor = 'pointer';
      }

      var w = width | 0;
      var h = height | 0;
      if (w <= 0 || h <= 0) return;

      var canvas = Module && Module['canvas'] ? Module['canvas'] : null;
      if (!canvas) return;

      // Keep render resolution fixed regardless of browser resize/fullscreen.
      if (typeof Module !== 'undefined') {
        Module.matchWebGLToCanvasSize = false;
      }
      canvas.width = w;
      canvas.height = h;
      if (Module.setCanvasSize) {
        Module.setCanvasSize(w, h);
      }

      // Keep canvas fully visible with predictable sizing.
      var viewportW = window.innerWidth || document.documentElement.clientWidth || w;
      var viewportH = window.innerHeight || document.documentElement.clientHeight || h;
      var dpr = window.devicePixelRatio || 1;

      // "Windowed/minimized" target size: fixed visual size based on device pixel density.
      var baseCssW = Math.round(w / dpr);
      var baseCssH = Math.round(h / dpr);
      var maxCssW = Math.floor(viewportW * 0.96);
      var maxCssH = Math.floor(viewportH * 0.96);
      var fitScale = Math.min(1, maxCssW / baseCssW, maxCssH / baseCssH);
      var windowedCssW = Math.max(1, Math.round(baseCssW * fitScale));
      var windowedCssH = Math.max(1, Math.round(baseCssH * fitScale));

      // Fullscreen target size: fill viewport while preserving 16:9.
      var fullScale = Math.min(viewportW / w, viewportH / h);
      var fullscreenCssW = Math.max(1, Math.round(w * fullScale));
      var fullscreenCssH = Math.max(1, Math.round(h * fullScale));

      var isFullscreen = !!document.fullscreenElement;
      var cssW = isFullscreen ? fullscreenCssW : windowedCssW;
      var cssH = isFullscreen ? fullscreenCssH : windowedCssH;

      canvas.style.display = 'block';
      canvas.style.position = 'fixed';
      canvas.style.left = '50%';
      canvas.style.top = '50%';
      canvas.style.transform = 'translate(-50%, -50%)';
      canvas.style.width = cssW + 'px';
      canvas.style.height = cssH + 'px';
      canvas.style.maxWidth = 'none';
      canvas.style.maxHeight = 'none';
      canvas.style.objectFit = 'fill';
      canvas.style.zIndex = '1';
      ensureFullscreenButton();
    } catch (e) {
      console.warn('SetWebGLFixedResolution failed:', e);
    }
  }
});
