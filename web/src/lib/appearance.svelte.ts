/**
 * Light or dark. By default the UI follows the system, the way Apple's apps do; a browser can
 * pin one instead, from Settings. The colours themselves are `light-dark()` pairs in
 * layout.css, picked by `color-scheme`: this module only says which scheme is wanted, as
 * `data-theme` on <html>. app.html applies a pinned choice before first paint, so a dark
 * browser never flashes light.
 */

export const APPEARANCES = ['auto', 'light', 'dark'] as const;
export type Appearance = (typeof APPEARANCES)[number];

/** Read by the inline script in app.html too: keep the two in step. */
export const APPEARANCE_KEY = 'yass.appearance';

function isAppearance(value: unknown): value is Appearance {
	return typeof value === 'string' && (APPEARANCES as readonly string[]).includes(value);
}

function initial(): Appearance {
	try {
		const stored = localStorage.getItem(APPEARANCE_KEY);
		if (isAppearance(stored)) return stored;
	} catch {
		// private mode or no storage: follow the system
	}
	return 'auto';
}

/** Sets the scheme on the page itself: nothing for auto, so the system's applies. */
function apply(mode: Appearance) {
	if (typeof document === 'undefined') return;
	if (mode === 'auto') delete document.documentElement.dataset.theme;
	else document.documentElement.dataset.theme = mode;
}

let current = $state<Appearance>(typeof window === 'undefined' ? 'auto' : initial());

export const appearance = {
	get mode(): Appearance {
		return current;
	},
	set(mode: Appearance) {
		current = mode;
		apply(mode);
		try {
			if (mode === 'auto') localStorage.removeItem(APPEARANCE_KEY);
			else localStorage.setItem(APPEARANCE_KEY, mode);
		} catch {
			// keep it for this visit at least
		}
	}
};
