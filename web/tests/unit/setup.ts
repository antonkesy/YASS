import '@testing-library/svelte/vitest';
import '@testing-library/jest-dom/vitest';
import { escape } from '$lib/keys.svelte';

// +layout.svelte's Escape-to-newest-layer listener; components render here without the layout.
window.addEventListener('keydown', (e) => {
	if (escape(e)) e.preventDefault();
});
