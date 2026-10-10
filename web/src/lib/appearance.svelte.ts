/** Colours are `light-dark()` pairs in layout.css; app.html applies the choice before paint. */

export const APPEARANCES = ['auto', 'light', 'dark'] as const;
export type Appearance = (typeof APPEARANCES)[number];

export const APPEARANCE_KEY = 'yass.appearance';

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

function apply(mode: Appearance) {
	if (typeof document === 'undefined') return;
	if (mode === 'auto') delete document.documentElement.dataset.theme;
	else document.documentElement.dataset.theme = mode;
}

let current = $state<Appearance>(typeof window === 'undefined' ? DEFAULT_APPEARANCE : initial());

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
	get dark(): boolean {
		return current === 'auto' ? systemDark : current === 'dark';
	},
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
