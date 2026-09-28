import { writable } from 'svelte/store';

/**
 * Which session the user just came back from, so home can slide in from the left and
 * highlight that scan. Held in memory rather than in the URL: it is a hint about the
 * transition, not part of the address, and `/?from=<id>` would stay in the address bar
 * and in history long after the animation finished.
 */
function create() {
	const { subscribe, set } = writable<string | null>(null);
	return {
		subscribe,
		leaving(sessionId: string) {
			set(sessionId);
		},
		/** Reads the hint and clears it, so it plays once. */
		take() {
			let value: string | null = null;
			subscribe((v) => (value = v))();
			set(null);
			return value as string | null;
		}
	};
}

export const cameFrom = create();
