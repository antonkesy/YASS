import { writable, type Readable } from 'svelte/store';

/**
 * This browser's localStorage, or `undefined` where there is none. Even reading the
 * `localStorage` getter throws when site data is blocked, so this is the one place that touches it.
 */
export function browserStorage(): Storage | undefined {
	try {
		return typeof window === 'undefined' ? undefined : window.localStorage;
	} catch {
		return undefined;
	}
}

/** How a persisted value is kept tidy. */
export type PersistOptions<T> = {
	/**
	 * Run over the value as it is loaded and again on every set, so the store and storage only
	 * ever hold what it lets through: unknown keys from an older build, empty values.
	 */
	clean?: (value: T) => T;
	/** Remove the key rather than store an object with nothing in it. */
	removeEmpty?: boolean;
};

/** A stored JSON value, or `fallback` when there is none or it is unusable. */
export function load<T>(storage: Storage | undefined, key: string, fallback: T): T {
	try {
		const raw = storage?.getItem(key);
		if (!raw) return fallback;
		const parsed: unknown = JSON.parse(raw);
		if (!parsed || typeof parsed !== 'object') return fallback;
		// Merged over the defaults, not used as-is: a browser that stored these before a
		// preference existed would otherwise read it as undefined, and the screens that
		// consume it disagree about what undefined means. Arrays (the presets) are
		// replaced wholesale — merging one key-by-key would splice the defaults back in. A
		// value of the wrong shape altogether, a list where a record belongs or the other way
		// round, is no use to anyone and gives way to the default.
		if (Array.isArray(fallback) !== Array.isArray(parsed)) return fallback;
		if (Array.isArray(parsed)) return parsed as T;
		return { ...fallback, ...parsed };
	} catch {
		return fallback;
	}
}

export type Persisted<T> = Readable<T> & {
	set(value: T): void;
	reset(): void;
};

/**
 * A store kept in this browser's storage under `key`: read once when created, written on
 * every set. Writing can fail (private mode, a full quota); the value then lives in memory
 * for this visit, which is all anyone could ask of it.
 */
export function persisted<T>(
	storage: Storage | undefined,
	key: string,
	fallback: T,
	{ clean = (value) => value, removeEmpty = false }: PersistOptions<T> = {}
): Persisted<T> {
	const store = writable<T>(clean(load(storage, key, fallback)));
	function set(value: T) {
		const next = clean(value);
		store.set(next);
		try {
			if (removeEmpty && typeof next === 'object' && next && Object.keys(next).length === 0)
				storage?.removeItem(key);
			else storage?.setItem(key, JSON.stringify(next));
		} catch {
			// private mode or quota: keep the in-memory value
		}
	}
	return { subscribe: store.subscribe, set, reset: () => set(fallback) };
}
