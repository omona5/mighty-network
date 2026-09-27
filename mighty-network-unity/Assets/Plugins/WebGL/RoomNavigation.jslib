mergeInto(LibraryManager.library, {
  MightySetPath: function(path) {
    // Keep explicit connection query options, but never put them in shared links.
    window.history.replaceState(null, '', UTF8ToString(path) + window.location.search);
  },
  MightyCopyInvite: function(code, receiver) {
    var target = UTF8ToString(receiver);
    var link = window.location.origin + '/room/' + encodeURIComponent(UTF8ToString(code));
    var report = function(ok) { SendMessage(target, 'OnInviteCopied', ok ? 'success' : 'failure'); };
    var fallback = function() {
      var field = document.createElement('textarea');
      field.value = link;
      field.style.position = 'fixed';
      field.style.opacity = '0';
      document.body.appendChild(field);
      field.select();
      try { report(document.execCommand('copy')); }
      catch (_) { report(false); }
      finally { field.remove(); }
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(link).then(function() { report(true); }, fallback);
    } else fallback();
  }
});
