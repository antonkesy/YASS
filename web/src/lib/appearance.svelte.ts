/**
 * Light or dark. The UI is light unless this browser chose otherwise in Settings: dark, or
 * automatic, which follows the system. The colours themselves are `light-dark()` pairs in
 * layout.css, picked by `color-scheme`: this module only says which scheme is wanted, as
 * `data-theme` on <html> (none for automatic). app.html applies the choice before first
 * paint, so nothing flashes the other scheme.
 */

export const APPEARANCES = ['auto', 'light', 'dark'] as const;
export type Appearance = (typeof APPEARANCES)[number];

/** Read by the inline script in app.html too: keep the two in step. */
export const APPEARANCE_KEY = 'yass.appearance';

/** What a browser that never chose gets. Not stored: the key's absence means this. */
export const DEFAULT_APPEARANCE: Appearance = 'light';

function isAppearance(value: unknown): value is Appearance {
	return typeof value === 'string' && (APPEARANCES as readonly string[]).includes(value);
}

function initial(): Appearance {
	try {
		const stored = localStorage.getItem(APPEARANCE_KEY);
		if (isAppearance(stored)) return stored;
	} catch {
		// private mode or no storage: the default
	}
	return DEFAULT_APPEARANCE;
}

/** Sets the scheme on the page itself: nothing for auto, so the system's applies. */
function apply(mode: Appearance) {
	if (typeof document === 'undefined') return;
	if (mode === 'auto') delete document.documentElement.dataset.theme;
	else document.documentElement.dataset.theme = mode;
}

let current = $state<Appearance>(typeof window === 'undefined' ? DEFAULT_APPEARANCE : initial());

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
	/** The other look from the one on screen, chosen outright (automatic is Settings' to pick). */
	toggle() {
		this.set(this.dark ? 'light' : 'dark');
	},
	set(mode: Appearance) {
		current = mode;
		apply(mode);
		try {
			if (mode === DEFAULT_APPEARANCE) localStorage.removeItem(APPEARANCE_KEY);
			else localStorage.setItem(APPEARANCE_KEY, mode);
		} catch {
			// keep it for this visit at least
		}
	}
};
