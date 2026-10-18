import { describe, expect, it } from 'vitest';
import { blockBrowserBehaviour, browserShortcut, historyButton } from '$lib/browser-keys';

const key = (
	key: string,
	mods: Partial<Record<'ctrlKey' | 'metaKey' | 'altKey' | 'shiftKey', boolean>> = {}
) => ({
	key,
	ctrlKey: false,
	metaKey: false,
	altKey: false,
	shiftKey: false,
	...mods
});

describe('browser shortcuts', () => {
	it('stops reload, print, find, zoom and history', () => {
		expect(browserShortcut(key('F5'))).toBe(true);
		expect(browserShortcut(key('F5', { ctrlKey: true }))).toBe(true);
		expect(browserShortcut(key('R', { ctrlKey: true }))).toBe(true);
		expect(browserShortcut(key('p', { metaKey: true }))).toBe(true);
		expect(browserShortcut(key('f', { ctrlKey: true }))).toBe(true);
		expect(browserShortcut(key('=', { ctrlKey: true }))).toBe(true);
		expect(browserShortcut(key('ArrowLeft', { altKey: true }))).toBe(true);
		expect(browserShortcut(key('BrowserBack'))).toBe(true);
	});

	it('stops WebView2’s history, downloads, favourites, new window and clear data', () => {
		for (const k of ['h', 'j', 'd', 'n']) {
			expect(browserShortcut(key(k, { ctrlKey: true }))).toBe(true);
		}
		expect(browserShortcut(key('Delete', { ctrlKey: true, shiftKey: true }))).toBe(true);
		expect(browserShortcut(key('Delete', { ctrlKey: true }))).toBe(false);
		expect(browserShortcut(key('Delete'))).toBe(false);
	});

	it('leaves editing and the app’s own keys alone', () => {
		for (const k of ['c', 'v', 'x', 'a', 'z', 'y']) {
			expect(browserShortcut(key(k, { ctrlKey: true }))).toBe(false);
		}
		expect(browserShortcut(key('r'))).toBe(false);
		expect(browserShortcut(key('ArrowLeft'))).toBe(false);
		expect(browserShortcut(key('Escape'))).toBe(false);
		expect(browserShortcut(key('F12'))).toBe(false);
	});

	it('knows the mouse’s back and forward buttons', () => {
		expect(historyButton({ button: 3 })).toBe(true);
		expect(historyButton({ button: 4 })).toBe(true);
		expect(historyButton({ button: 0 })).toBe(false);
	});

	it('stops only the default, and can be undone', () => {
		const undo = blockBrowserBehaviour(window);
		let seen = false;
		const listener = () => (seen = true);
		window.addEventListener('keydown', listener);
		const reload = new KeyboardEvent('keydown', { key: 'F5', cancelable: true });
		window.dispatchEvent(reload);
		expect(reload.defaultPrevented).toBe(true);
		expect(seen).toBe(true);

		undo();
		const again = new KeyboardEvent('keydown', { key: 'F5', cancelable: true });
		window.dispatchEvent(again);
		expect(again.defaultPrevented).toBe(false);
		window.removeEventListener('keydown', listener);
	});

	it('stops the middle button’s autoscroll, but not its paste into a field', () => {
		const undo = blockBrowserBehaviour(window);
		const input = document.body.appendChild(document.createElement('input'));
		try {
			const scroll = new MouseEvent('mousedown', { button: 1, bubbles: true, cancelable: true });
			document.body.dispatchEvent(scroll);
			expect(scroll.defaultPrevented).toBe(true);

			const paste = new MouseEvent('mousedown', { button: 1, bubbles: true, cancelable: true });
			input.dispatchEvent(paste);
			expect(paste.defaultPrevented).toBe(false);

			const left = new MouseEvent('mousedown', { button: 0, bubbles: true, cancelable: true });
			document.body.dispatchEvent(left);
			expect(left.defaultPrevented).toBe(false);
		} finally {
			undo();
			input.remove();
		}
	});

	it('lets nothing be dragged out of the window or dropped into it', () => {
		const undo = blockBrowserBehaviour(window);
		const img = document.body.appendChild(document.createElement('img'));
		const input = document.body.appendChild(document.createElement('input'));
		try {
			const out = new Event('dragstart', { bubbles: true, cancelable: true });
			img.dispatchEvent(out);
			expect(out.defaultPrevented).toBe(true);

			const text = new Event('dragstart', { bubbles: true, cancelable: true });
			input.dispatchEvent(text);
			expect(text.defaultPrevented).toBe(false);

			for (const type of ['dragover', 'drop']) {
				const file = new Event(type, { bubbles: true, cancelable: true });
				document.body.dispatchEvent(file);
				expect(file.defaultPrevented).toBe(true);
			}
		} finally {
			undo();
			img.remove();
			input.remove();
		}
	});
});
