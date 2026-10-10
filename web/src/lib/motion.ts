import { onMount } from 'svelte';
import type { Attachment } from 'svelte/attachments';
import { cubicIn } from 'svelte/easing';
import type { TransitionConfig } from 'svelte/transition';

/**
 * Leaving animations for dialogs. Entrances are CSS (`animate-card-in` and friends), but a
 * dialog closed by an `{#if}` is gone before CSS could animate it out, so the way out is a
 * Svelte transition. Kept short: closing should feel immediate, just not abrupt.
 */

export function reduced(): boolean {
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

let sliding = false;

/**
 * Whether a screen change is being animated by a view transition right now (see
 * +layout.svelte). A screen reads it once as it mounts: while the transition already slides it
 * in, its own entrance animation would be a second slide on top of the first. Without view
 * transitions, and on a cold load, it is false and the screen animates itself as before.
 */
export const screenTransition = {
	start: () => (sliding = true),
	end: () => (sliding = false),
	get active() {
		return sliding;
	}
};

/** A duration in milliseconds, or none at all for anyone who asked for less motion. */
export function ms(duration: number): number {
	return reduced() ? 0 : duration;
}

/**
 * Plays a one-off animation on an element that is already on screen — a pick, a nudge —
 * through the Web Animations API, so it never fights the element's own CSS animations.
 */
export function play(el: Element, keyframes: Keyframe[], duration: number): void {
	if (reduced() || typeof el.animate !== 'function') return;
	el.animate(keyframes, { duration, easing: 'cubic-bezier(0.2, 0.8, 0.2, 1)' });
}

/** A pressed pill lighting up: squeezed, then a little past its size, then settled. */
export const pickKeyframes: Keyframe[] = [
	{ transform: 'scale(0.92)' },
	{ transform: 'scale(1.07)', offset: 0.55 },
	{ transform: 'scale(1)' }
];

/**
 * For a row of pills: the one `lit()` names gives a small springy bounce as it lights up. Not
 * on load: a screen that opens with one already lit has nothing to answer. Call it while the
 * component is being set up; what it returns is each pill's attachment, by the pill's id.
 */
export function bounceWhenPicked(lit: () => string | null): (id: string) => Attachment {
	let settled = false;
	onMount(() => {
		settled = true;
	});
	return (id) => (el) => {
		if (lit() === id && settled) play(el, pickKeyframes, 420);
	};
}

/** The paper on the stage taking in new settings: a small hop and tilt, and back. */
export const hopKeyframes: Keyframe[] = [
	{ transform: 'translateY(0) rotate(0)' },
	{ transform: 'translateY(-10px) rotate(-2deg)', offset: 0.4 },
	{ transform: 'translateY(2px) rotate(0.5deg)', offset: 0.75 },
	{ transform: 'translateY(0) rotate(0)' }
];
