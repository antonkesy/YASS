import { get, writable } from 'svelte/store';

/** Kept in memory, not the URL: `/?from=<id>` would linger in the address bar and history. */
function create() {
	const store = writable<string | null>(null);
	const { subscribe, set } = store;
	return {
		subscribe,
		leaving(sessionId: string) {
			set(sessionId);
		},
		take() {
			const value = get(store);
			set(null);
			return value;
		}
	};
}

export const cameFrom = create();
