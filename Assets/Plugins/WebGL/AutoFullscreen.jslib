mergeInto(LibraryManager.library, {
  ConfigureWebGLAutoFullscreen: function () {
    try {
      function getTarget() {
        var canvas = Module && Module['canvas'] ? Module['canvas'] : null;
        return (canvas && canvas.parentElement) ? canvas.parentElement : (canvas || document.documentElement);
      }

      function tryFullscreen() {
        try {
          if (document.fullscreenElement) return;

          var target = getTarget();
          if (target.requestFullscreen) {
            target.requestFullscreen();
          } else if (target.webkitRequestFullscreen) {
            target.webkitRequestFullscreen();
          } else if (target.msRequestFullscreen) {
            target.msRequestFullscreen();
          }
        } catch (e) {
          console.warn('ConfigureWebGLAutoFullscreen tryFullscreen failed:', e);
        }
      }

      // Best-effort immediate attempt (works only where browser allows).
      setTimeout(tryFullscreen, 0);

      // Browser-compliant fallback: request fullscreen on first user gesture.
      var opts = { once: true, passive: true };
      document.addEventListener('pointerdown', tryFullscreen, opts);
      document.addEventListener('touchstart', tryFullscreen, opts);
      document.addEventListener('keydown', tryFullscreen, opts);
      document.addEventListener('mousedown', tryFullscreen, opts);
    } catch (e) {
      console.warn('ConfigureWebGLAutoFullscreen failed:', e);
    }
  },

  RequestWebGLFullscreen: function () {
    try {
      var canvas = Module['canvas'];
      var target = (canvas && canvas.parentElement) ? canvas.parentElement : (canvas || document.documentElement);

      if (target.requestFullscreen) {
        target.requestFullscreen();
      } else if (target.webkitRequestFullscreen) {
        target.webkitRequestFullscreen();
      } else if (target.msRequestFullscreen) {
        target.msRequestFullscreen();
      }
    } catch (e) {
      console.warn('RequestWebGLFullscreen failed:', e);
    }
  }
});
