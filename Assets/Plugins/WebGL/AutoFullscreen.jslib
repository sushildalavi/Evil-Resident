mergeInto(LibraryManager.library, {
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
