mergeInto(LibraryManager.library, {
  // 返回画布的 CSS 尺寸 × devicePixelRatio，也就是浏览器触摸事件的坐标系宽度。
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
  }
});
