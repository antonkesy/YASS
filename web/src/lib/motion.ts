import { onMount } from 'svelte';
import type { Attachment } from 'svelte/attachments';
import { cubicIn } from 'svelte/easing';
import type { TransitionConfig } from 'svelte/transition';

// A dialog closed by `{#if}` is gone before CSS can animate it out, so exits are transitions.

export function reduced(): boolean {
	return (
		typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches
	);
}

export const backdropOut: (node: Element) => TransitionConfig = () => ({
	duration: reduced() ? 0 : 180,
	css: (t) => `opacity: ${t}`
});

export const cardOut: (node: Element) => TransitionConfig = () => ({
	duration: reduced() ? 0 : 160,
	easing: cubicIn,
	css: (t) => `opacity: ${t}; transform: translateY(${(1 - t) * 12}px) scale(${0.96 + 0.04 * t})`
});

let sliding = false;

/**
 * True while a view transition (see +layout.svelte) slides the screen in, so the screen skips
 * its own entrance animation instead of sliding twice.
 */
export const screenTransition = {
	start: () => (sliding = true),
	end: () => (sliding = false),
	get active() {
		return sliding;
	}
};

export function ms(duration: number): number {
	return reduced() ? 0 : duration;
}

/** Web Animations API, so it never fights the element's own CSS animations. */
export function play(el: Element, keyframes: Keyframe[], duration: number): void {
	if (reduced() || typeof el.animate !== 'function') return;
	el.animate(keyframes, { duration, easing: 'cubic-bezier(0.2, 0.8, 0.2, 1)' });
}

export const pickKeyframes: Keyframe[] = [
	{ transform: 'scale(0.92)' },
	{ transform: 'scale(1.07)', offset: 0.55 },
	{ transform: 'scale(1)' }
];

/** Does not bounce for a pill already lit on mount. Call during component setup. */
export function bounceWhenPicked(lit: () => string | null): (id: string) => Attachment {
	let settled = false;
	onMount(() => {
		settled = true;
	});
	return (id) => (el) => {
		if (lit() === id && settled) play(el, pickKeyframes, 420);
	};
}

export const hopKeyframes: Keyframe[] = [
	{ transform: 'translateY(0) rotate(0)' },
	{ transform: 'translateY(-10px) rotate(-2deg)', offset: 0.4 },
	{ transform: 'translateY(2px) rotate(0.5deg)', offset: 0.75 },
	{ transform: 'translateY(0) rotate(0)' }
];
