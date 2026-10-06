mergeInto(LibraryManager.library, {
  MightyNicknameOpen: function(receiverPtr, valuePtr, titlePtr, okPtr, cancelPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var root = document.createElement('div');
    root.id = 'mighty-nickname-input';
    root.style.cssText = 'position:fixed;inset:0;z-index:2147483647;background:#000c;display:flex;align-items:center;justify-content:center';
    var panel = document.createElement('div');
    panel.style.cssText = 'width:min(440px,85vw);padding:24px;background:#20252c;color:white;font:20px sans-serif;border-radius:10px';
    var title = document.createElement('label');
    title.textContent = UTF8ToString(titlePtr);
    var input = document.createElement('input');
    input.type = 'text';
    input.value = UTF8ToString(valuePtr);
    input.autocomplete = 'off';
    input.style.cssText = 'box-sizing:border-box;width:100%;margin:16px 0;padding:12px;font:22px sans-serif';
    title.appendChild(input);
    panel.appendChild(title);
    var composing = false;
    var finished = false;
    function finish(accept) {
      if (finished || (accept && composing)) return;
      finished = true;
      SendMessage(receiver, accept ? 'Accept' : 'Cancel', accept ? input.value.normalize('NFC') : '');
      root.remove();
    }
    input.addEventListener('compositionstart', function() { composing = true; });
    input.addEventListener('compositionend', function() { composing = false; });
    input.addEventListener('keydown', function(e) {
      e.stopPropagation();
      if (e.isComposing || composing || e.keyCode === 229) return;
      if (e.key === 'Enter') { e.preventDefault(); finish(true); }
      if (e.key === 'Escape') { e.preventDefault(); finish(false); }
    });
    [UTF8ToString(okPtr), UTF8ToString(cancelPtr)].forEach(function(label, i) {
      var button = document.createElement('button');
      button.textContent = label;
      button.style.cssText = 'padding:10px 24px;margin-right:12px;font:18px sans-serif';
      button.addEventListener('click', function() { finish(i === 0); });
      panel.appendChild(button);
    });
    root.appendChild(panel);
    (document.fullscreenElement || document.body).appendChild(root);
    input.focus();
    input.select();
  },
  MightyNicknameClose: function() {
    var root = document.getElementById('mighty-nickname-input');
    if (root) root.remove();
  }
});
