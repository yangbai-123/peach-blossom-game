mergeInto(LibraryManager.library, {
  // 联机：PeerJS（WebRTC 数据通道），信令走 0.peerjs.com 公共服务器。
  // 建立连接后两端直接 P2P 通信，不需要自建服务器。

  DSH_NetHost: function () {
    if (typeof window.Peer === 'undefined') {
      SendMessage('Net', 'OnNetError', 'PeerJS 未加载');
      return;
    }

    var id = 'tao' + (1000 + Math.floor(Math.random() * 9000));

    try {
      window._net = new window.Peer(id);
    } catch (e) {
      SendMessage('Net', 'OnNetError', '创建房间失败');
      return;
    }

    window._net.on('open', function (pid) {
      SendMessage('Net', 'OnReady', pid);
    });

    window._net.on('connection', function (c) {
      window._conn = c;
      c.on('open', function () { SendMessage('Net', 'OnPeerJoined', ''); });
      c.on('data', function (d) { SendMessage('Net', 'OnData', String(d)); });
      c.on('close', function () { SendMessage('Net', 'OnPeerLeft', ''); });
    });

    window._net.on('error', function (e) {
      SendMessage('Net', 'OnNetError', String(e && e.type ? e.type : e));
    });
  },

  DSH_NetJoin: function (ptr) {
    var room = UTF8ToString(ptr);

    if (typeof window.Peer === 'undefined') {
      SendMessage('Net', 'OnNetError', 'PeerJS 未加载');
      return;
    }

    try {
      window._net = new window.Peer();
    } catch (e) {
      SendMessage('Net', 'OnNetError', '连接失败');
      return;
    }

    window._net.on('open', function () {
      var c = window._net.connect(room, { reliable: true });
      window._conn = c;

      c.on('open', function () { SendMessage('Net', 'OnPeerJoined', ''); });
      c.on('data', function (d) { SendMessage('Net', 'OnData', String(d)); });
      c.on('close', function () { SendMessage('Net', 'OnPeerLeft', ''); });
    });

    window._net.on('error', function (e) {
      SendMessage('Net', 'OnNetError', String(e && e.type ? e.type : e));
    });
  },

  DSH_NetSend: function (ptr) {
    var s = UTF8ToString(ptr);
    if (window._conn && window._conn.open) {
      try { window._conn.send(s); } catch (e) { }
    }
  },

  DSH_NetClose: function () {
    try { if (window._conn) window._conn.close(); } catch (e) { }
    try { if (window._net) window._net.destroy(); } catch (e) { }
    window._conn = null;
    window._net = null;
  }
});
