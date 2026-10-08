// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		// interface Error {}
		// interface Locals {}
		// interface PageData {}
		interface PageState {
			/** Settings opens with the new-preset name field ready, from the home screen's Save as preset. */
			newPreset?: boolean;
		}
		// interface Platform {}
	}
}

export {};
