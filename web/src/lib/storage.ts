import { writable, type Readable } from 'svelte/store';

/** Even reading the `localStorage` getter throws when site data is blocked. */
export function browserStorage(): Storage | undefined {
	try {
		return typeof window === 'undefined' ? undefined : window.localStorage;
	} catch {
		return undefined;
	}
}

export type PersistOptions<T> = {
	clean?: (value: T) => T;
	removeEmpty?: boolean;
};

export function load<T>(storage: Storage | undefined, key: string, fallback: T): T {
	try {
		const raw = storage?.getItem(key);
		if (!raw) return fallback;
		const parsed: unknown = JSON.parse(raw);
		if (!parsed || typeof parsed !== 'object') return fallback;
		// Merged over the defaults so preferences added later are not undefined. Arrays are
		// replaced wholesale, and a value of the wrong shape gives way to the default.
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
