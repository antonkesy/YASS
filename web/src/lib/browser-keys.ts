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

function isEditable(target: EventTarget | null): boolean {
	return (
		target instanceof HTMLInputElement ||
		target instanceof HTMLTextAreaElement ||
		(target instanceof HTMLElement && target.isContentEditable)
	);
}

/** Keeps the desktop app from behaving like a browser tab. Returns the undo. */
export function blockBrowserBehaviour(target: Window = window): () => void {
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
	// The app has no HTML drag and drop: a dragged image or link only leaves as a browser one,
	// and a file dropped on the window would replace the app with it.
	const drag = (e: DragEvent) => {
		if (!isEditable(e.target)) e.preventDefault();
	};
	const drop = (e: DragEvent) => {
		e.preventDefault();
		if (e.dataTransfer) e.dataTransfer.dropEffect = 'none';
	};
	const listeners = [
		['keydown', key],
		['mouseup', mouse],
		['wheel', wheel],
		['dragstart', drag],
		['dragover', drop],
		['drop', drop]
	] as const;
	for (const [type, listener] of listeners)
		target.addEventListener(type, listener as EventListener, { capture: true, passive: false });
	return () => {
		for (const [type, listener] of listeners)
			target.removeEventListener(type, listener as EventListener, { capture: true });
	};
}
