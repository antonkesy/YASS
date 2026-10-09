import '@testing-library/svelte/vitest';
import '@testing-library/jest-dom/vitest';
import { escape } from '$lib/keys.svelte';

// What +layout.svelte does in the app: one window listener hands Escape to the newest layer.
// Components are rendered here without the layout, so the tests install the same listener.
window.addEventListener('keydown', (e) => {
	if (escape(e)) e.preventDefault();
});
