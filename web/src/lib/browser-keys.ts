type Keys = Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'altKey'> &
	Partial<Pick<KeyboardEvent, 'shiftKey'>>;

const plain = new Set([
	'F3',
	'F5',
	'F7',
	'BrowserBack',
	'BrowserForward',
	'BrowserRefresh',
	'BrowserHome',
	'BrowserSearch'
]);
// Reload, print, find, save page, open file, view source, zoom, and WebView2's history, downloads,
// favourites and new window.
const withCtrl = new Set([
	'r',
	'p',
	'f',
	'g',
	's',
	'o',
	'u',
	'h',
	'j',
	'd',
	'n',
	'+',
	'=',
	'-',
	'_',
	'0'
]);

/**
 * Keys the desktop webview would act on as a browser: reload, print, find, zoom and history.
 * Only the default is stopped, so the app's own shortcuts (Ctrl+S, Alt+arrows) still run.
 */
export function browserShortcut(e: Keys): boolean {
	const key = e.key.length === 1 ? e.key.toLowerCase() : e.key;
	if (plain.has(key)) return true;
	const ctrl = e.ctrlKey || e.metaKey;
	// Ctrl+Shift+Delete: WebView2's clear browsing data.
	if (ctrl && !e.altKey)
		return withCtrl.has(key) || key === 'F5' || (key === 'Delete' && e.shiftKey === true);
	return e.altKey && !ctrl && (key === 'ArrowLeft' || key === 'ArrowRight' || key === 'Home');
}

/** The mouse's back and forward buttons. */
export function historyButton(e: Pick<MouseEvent, 'button'>): boolean {
	return e.button === 3 || e.button === 4;
}

/** Keeps the desktop app from behaving like a browser tab. Returns the undo. */
export function blockBrowserShortcuts(target: Window = window): () => void {
	const key = (e: KeyboardEvent) => {
		if (browserShortcut(e)) e.preventDefault();
	};
	const mouse = (e: MouseEvent) => {
		if (historyButton(e)) e.preventDefault();
	};
	const wheel = (e: WheelEvent) => {
		// Ctrl+wheel, and a touchpad pinch, would zoom the whole window.
		if (e.ctrlKey) e.preventDefault();
	};
	target.addEventListener('keydown', key, { capture: true });
	target.addEventListener('mouseup', mouse, { capture: true });
	target.addEventListener('wheel', wheel, { capture: true, passive: false });
	return () => {
		target.removeEventListener('keydown', key, { capture: true });
		target.removeEventListener('mouseup', mouse, { capture: true });
		target.removeEventListener('wheel', wheel, { capture: true });
	};
}
