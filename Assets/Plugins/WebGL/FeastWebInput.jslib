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
    var compositionEndedAt = -1000;
    var coarsePointer = window.matchMedia && window.matchMedia("(pointer: coarse)").matches;
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
      compositionEndedAt = performance.now();
      notify(0);
    });
    input.addEventListener('blur', function () { composing = false; });
    input.addEventListener('input', function () { notify(0); });
    input.addEventListener('keydown', function (event) {
      event.stopPropagation();
      // Some browsers emit the confirmation keydown after compositionend with keyCode 229.
      if (composing || event.isComposing || event.keyCode === 229 ||
          (event.key === "Enter" && performance.now() - compositionEndedAt <= 80)) return;
      var action = actions[event.key];
      if (!action && event.key !== 'Tab') return;
      event.preventDefault();
      if (action && !event.repeat) notify(action);
    });
    input.addEventListener('keypress', function (event) { event.stopPropagation(); });
    input.addEventListener('keyup', function (event) { event.stopPropagation(); });

    function focusInput() {
      if (!input.hidden && !coarsePointer) input.focus({ preventScroll: true });
    }
    // Mobile users explicitly tap the HTML input; unrelated game taps must not open the keyboard.
    if (!coarsePointer) canvas.addEventListener("click", focusInput);
    Module.feastWebInput = { input: input, focusInput: focusInput, coarsePointer: coarsePointer,
      isComposing: function () { return composing || performance.now() - compositionEndedAt <= 80; } };
  },

  FeastWebInput_PlaceResponsive: function (x, y, width, height, fontHeight) {
    var state = Module.feastWebInput;
    if (!state) return;
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
    // Bounds and font size come from the same responsive UI Toolkit node.
    var fontSize = Math.max(16, fontHeight * canvas.height);
    input.style.left = canvas.left + x * canvas.width + 'px';
    input.style.top = canvas.top + y * canvas.height + 'px';
    input.style.width = width * canvas.width + 'px';
    input.style.height = height * canvas.height + 'px';
    input.style.fontSize = fontSize + 'px';
    input.style.padding = '0 ' + Math.max(6, fontSize * 0.4) + 'px';
    if (input.hidden) {
      input.hidden = false;
      state.focusInput();
    }
  },

  FeastWebInput_IsComposing: function () {
    var state = Module.feastWebInput;
    return state && state.isComposing() ? 1 : 0;
  },

  FeastWebInput_VisibleBottom: function () {
    var viewport = window.visualViewport;
    if (!viewport) return 1;
    var canvas = Module.canvas.getBoundingClientRect();
    if (canvas.height <= 0) return 1;
    // If the host already resized its canvas for the keyboard, this evaluates to 1.
    return Math.max(0, Math.min(1, (viewport.offsetTop + viewport.height - canvas.top) / canvas.height));
  },

  FeastWebInput_Orientation: function () {
    var state = Module.feastWebInput;
    if (!state || !state.coarsePointer || !window.screen) return -1;
    var orientation = window.screen.orientation;
    if (orientation && orientation.type) return orientation.type.indexOf('portrait') === 0 ? 1 : 0;
    if (typeof window.orientation === 'number') return Math.abs(window.orientation) === 90 ? 0 : 1;
    return -1;
  },

  FeastWebInput_Focus: function () {
    if (Module.feastWebInput) Module.feastWebInput.focusInput();
  },

  FeastWebInput_SetValue: function (text) {
    if (!Module.feastWebInput) return;
    var input = Module.feastWebInput.input;
    var value = UTF8ToString(text);
    if (input.value !== value) input.value = value;
  },

  FeastWebInput_Hide: function () {
    if (!Module.feastWebInput) return;
    var input = Module.feastWebInput.input;
    var restoreFocus = document.activeElement === input;
    input.hidden = true;
    if (restoreFocus) Module.canvas.focus({ preventScroll: true });
  },

  FeastWebInput_Destroy: function () {
    var state = Module.feastWebInput;
    if (!state) return;
    Module.canvas.removeEventListener("click", state.focusInput);
    state.input.remove();
    delete Module.feastWebInput;
  }
});
