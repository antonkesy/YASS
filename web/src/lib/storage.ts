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
