mergeInto(LibraryManager.library, {
  FeastWebInput_Create: function (callback) {
    var canvas = Module.canvas;
    var input = document.createElement('input');
    input.type = 'text';
    input.maxLength = 48;
    input.lang = 'ja';
    input.autocomplete = 'off';
    input.autocapitalize = 'off';
    input.enterKeyHint = 'send';
    input.spellcheck = false;
    input.setAttribute('aria-label', '料理名');
    input.hidden = true;
    Object.assign(input.style, {
      position: 'fixed',
      zIndex: '10',
      boxSizing: 'border-box',
      border: '0',
      margin: '0',
      background: 'transparent',
      color: '#401b13',
      fontFamily: '"Noto Sans JP", system-ui, sans-serif'
    });
    document.body.appendChild(input);

    var composing = false;
    // Matches FeastApp.WebInputAction. Tab is consumed by the game without an action.
    var actions = { Enter: 1, Escape: 2, F1: 3, F3: 4, F5: 5 };

    function notify(action) {
      if (input.hidden) return;
      var text = 0;
      if (action === 0 || action === 1) {
        var size = lengthBytesUTF8(input.value) + 1;
        text = _malloc(size);
        stringToUTF8(input.value, text, size);
      }
      try {
        {{{ makeDynCall('vii', 'callback') }}}(action, text);
      } finally {
        if (text) _free(text);
      }
    }

    input.addEventListener('compositionstart', function () { composing = true; });
    input.addEventListener('compositionend', function () {
      composing = false;
      notify(0);
    });
    input.addEventListener('blur', function () { composing = false; });
    input.addEventListener('input', function () { notify(0); });
    input.addEventListener('keydown', function (event) {
      event.stopPropagation();
      // Some browsers emit the confirmation keydown after compositionend with keyCode 229.
      if (composing || event.isComposing || event.keyCode === 229) return;
      var action = actions[event.key];
      if (!action && event.key !== 'Tab') return;
      event.preventDefault();
      if (action && !event.repeat) notify(action);
    });
    input.addEventListener('keypress', function (event) { event.stopPropagation(); });
    input.addEventListener('keyup', function (event) { event.stopPropagation(); });

    function focusInput() {
      if (!input.hidden) input.focus({ preventScroll: true });
    }
    canvas.addEventListener('click', focusInput);
    Module.feastWebInput = { input: input, focusInput: focusInput };
  },

  FeastWebInput_Place: function (x, y, width, height) {
    var state = Module.feastWebInput;
    var input = state.input;
    var canvasElement = Module.canvas;
    // Fullscreen containers only show their own descendants.
    var fullscreen = document.fullscreenElement;
    var parent = fullscreen && fullscreen !== canvasElement && fullscreen.contains(canvasElement)
      ? fullscreen : document.body;
    if (input.parentNode !== parent) {
      var wasFocused = document.activeElement === input;
      parent.appendChild(input);
      if (wasFocused) state.focusInput();
    }
    var canvas = canvasElement.getBoundingClientRect();
    var pixelsPerUnit = width * canvas.width / 600;
    input.style.left = canvas.left + x * canvas.width + 'px';
    input.style.top = canvas.top + y * canvas.height + 'px';
    input.style.width = width * canvas.width + 'px';
    input.style.height = height * canvas.height + 'px';
    input.style.fontSize = 23 * pixelsPerUnit + 'px';
    input.style.padding = '0 ' + 18 * pixelsPerUnit + 'px';
    if (input.hidden) {
      input.hidden = false;
      state.focusInput();
    }
  },

  FeastWebInput_SetValue: function (text) {
    var input = Module.feastWebInput.input;
    var value = UTF8ToString(text);
    if (input.value !== value) input.value = value;
  },

  FeastWebInput_Hide: function () {
    var input = Module.feastWebInput.input;
    var restoreFocus = document.activeElement === input;
    input.hidden = true;
    if (restoreFocus) Module.canvas.focus({ preventScroll: true });
  },

  FeastWebInput_Destroy: function () {
    var state = Module.feastWebInput;
    Module.canvas.removeEventListener('click', state.focusInput);
    state.input.remove();
    delete Module.feastWebInput;
  }
});
