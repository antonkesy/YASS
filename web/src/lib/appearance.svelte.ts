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

/** Whether the system asks for dark, kept up to date as it changes (at sunset, say). */
const query =
	typeof window !== 'undefined' && window.matchMedia
		? window.matchMedia('(prefers-color-scheme: dark)')
		: null;
let systemDark = $state(query?.matches ?? false);
query?.addEventListener('change', (e) => (systemDark = e.matches));

export const appearance = {
	get mode(): Appearance {
		return current;
	},
	/** Whether the UI is dark right now, whichever way that was decided. */
	get dark(): boolean {
		return current === 'auto' ? systemDark : current === 'dark';
	},
	/**
	 * The other look from the one on screen. Where that is what the system asks for anyway,
	 * the choice goes back to automatic rather than pinning it, so the switch never leaves
	 * a browser stuck on a scheme it would have had regardless.
	 */
	toggle() {
		const dark = !this.dark;
		this.set(dark === systemDark ? 'auto' : dark ? 'dark' : 'light');
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
