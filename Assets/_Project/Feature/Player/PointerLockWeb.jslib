// 브라우저의 실제 포인터 잠금 상태. Unity의 Cursor.lockState는 Esc나 브라우저 거부로 풀린 것을 반영하지 않는다.
mergeInto(LibraryManager.library, {
  PointerLock_IsLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  },

  // 우클릭은 발차기다. 캔버스에서 브라우저 메뉴가 뜨지 않게 한다.
  PointerLock_BlockContextMenu: function () {
    var canvas = Module.canvas || document.querySelector("canvas");
    if (canvas && !canvas.dataset.noContextMenu) {
      canvas.dataset.noContextMenu = "1";
      canvas.addEventListener("contextmenu", function (e) { e.preventDefault(); });
    }
  },
});
