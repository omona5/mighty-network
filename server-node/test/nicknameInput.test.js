const assert = require('assert');
const fs = require('fs');
const vm = require('vm');
const path = require('path');

// Exercise browser composition events without requiring a built Unity player.
const elements = [];
function element(tag) {
  const el = { tag, style: {}, children: [], events: {}, value: '',
    appendChild(child) { this.children.push(child); },
    addEventListener(name, fn) { this.events[name] = fn; },
    remove() { this.removed = true; }, focus() {}, select() {} };
  elements.push(el);
  return el;
}
const calls = [];
const library = {};
const context = {
  LibraryManager: { library }, mergeInto: Object.assign, UTF8ToString: s => s,
  SendMessage: (...args) => calls.push(args),
  document: { body: element('body'), createElement: element,
    getElementById: id => elements.find(e => e.id === id && !e.removed) }
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,
  '../../mighty-network-unity/Assets/Plugins/WebGL/NicknameInput.jslib'), 'utf8'), context);
library.MightyNicknameOpen('NicknameInput', '', 'Nickname', 'Confirm', 'Cancel');
const input = elements.find(e => e.tag === 'input');
const key = (key, composing = false) => ({ key, isComposing: composing,
  stopPropagation() {}, preventDefault() {} });
input.events.compositionstart();
input.value = 'ㅎ';
input.events.keydown(key('Enter', true));
assert.equal(calls.length, 0, 'IME Enter must not submit an incomplete nickname');
input.value = '\u1112\u1161\u11ab글';
input.events.compositionend();
input.events.keydown(key('Enter'));
assert.deepEqual(calls[0], ['NicknameInput', 'Accept', '한글']);
input.events.keydown(key('Enter'));
assert.equal(calls.length, 1, 'submit only once');
console.log('Nickname browser composition tests passed.');
