import { cubicIn } from 'svelte/easing';
import type { TransitionConfig } from 'svelte/transition';

/**
 * Leaving animations for dialogs. Entrances are CSS (`animate-card-in` and friends), but a
 * dialog closed by an `{#if}` is gone before CSS could animate it out, so the way out is a
 * Svelte transition. Kept short: closing should feel immediate, just not abrupt.
 */

function reduced(): boolean {
	return (
		typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches
	);
}

/** The dimmed backdrop behind a dialog, fading away. */
export const backdropOut: (node: Element) => TransitionConfig = () => ({
	duration: reduced() ? 0 : 180,
	css: (t) => `opacity: ${t}`
});

/** A dialog card sinking back a little as it fades: the entrance played in reverse, quicker. */
export const cardOut: (node: Element) => TransitionConfig = () => ({
	duration: reduced() ? 0 : 160,
	easing: cubicIn,
	css: (t) => `opacity: ${t}; transform: translateY(${(1 - t) * 12}px) scale(${0.96 + 0.04 * t})`
});
