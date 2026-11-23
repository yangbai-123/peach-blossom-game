mergeInto(LibraryManager.library, {
  // 画布的 CSS 尺寸 × devicePixelRatio，也就是浏览器触摸事件的坐标系尺寸。
  // Unity 的 Screen.width 是渲染目标尺寸，两者常常不一致，需要用它换算。
  DSH_CanvasInputWidth: function () {
    var c = document.querySelector('#unity-canvas');
    var dpr = window.devicePixelRatio || 1;
    var w = c && c.clientWidth ? c.clientWidth : window.innerWidth;
    return Math.floor(w * dpr);
  },

  DSH_CanvasInputHeight: function () {
    var c = document.querySelector('#unity-canvas');
    var dpr = window.devicePixelRatio || 1;
    var h = c && c.clientHeight ? c.clientHeight : window.innerHeight;
    return Math.floor(h * dpr);
  },

  // 1 = 粗指针设备（手机/平板），0 = 精确指针（鼠标）。
  // 比 UA 判断更可靠：带触摸屏的 Windows 笔记本仍然是 fine，手机一定是 coarse。
  DSH_IsTouchOnly: function () {
    try {
      if (window.matchMedia && window.matchMedia('(pointer: coarse)').matches) return 1;
      if (navigator.maxTouchPoints > 0 && !window.matchMedia('(pointer: fine)').matches) return 1;
    } catch (e) { }
    return 0;
  },

  // 请求全屏并锁定横屏。浏览器要求必须由用户手势触发（点击/触摸）。
  DSH_EnterLandscape: function () {
    var el = document.getElementById('unity-container')
      || document.getElementById('unity-canvas')
      || document.body;

    try {
      if (!document.fullscreenElement && !document.webkitFullscreenElement) {
        if (el.requestFullscreen) el.requestFullscreen();
        else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
      }
    } catch (e) { }

    try {
      if (screen.orientation && screen.orientation.lock) {
        var p = screen.orientation.lock('landscape');
        if (p && p.catch) p.catch(function () { });
      } else if (screen.lockOrientation) {
        screen.lockOrientation('landscape');
      }
    } catch (e) { }
  },

  DSH_IsPortrait: function () {
    return window.innerHeight > window.innerWidth ? 1 : 0;
  },

  DSH_IsFullscreen: function () {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  }
});
