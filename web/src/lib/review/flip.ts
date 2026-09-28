/**
 * First-Last-Invert-Play: measure where things are, let the DOM reorder, then animate
 * each one from where it was to where it is. Without it a drag-reorder makes pages jump
 * to their new slots with no sense of what moved where.
 */
export const FLIP_MS = 600;
export const FLIP_EASING = 'cubic-bezier(.2,.8,.2,1)';

export type Positions = Map<string, DOMRect>;

/** Where each keyed element currently is. */
export function measure(container: HTMLElement | null, key = 'data-index'): Positions {
	const positions: Positions = new Map();
	if (!container) return positions;
	for (const el of container.querySelectorAll<HTMLElement>(`[${key}]`)) {
		const id = el.getAttribute(key);
		if (id !== null) positions.set(id, el.getBoundingClientRect());
	}
	return positions;
}

/**
 * Animates each element from its remembered position to its current one. Returns how many
 * actually moved, so a caller can tell a real reorder from a no-op.
 */
export function play(container: HTMLElement | null, before: Positions, key = 'data-index'): number {
	if (!container || typeof container.animate !== 'function') return 0;

	let moved = 0;
	for (const el of container.querySelectorAll<HTMLElement>(`[${key}]`)) {
		const id = el.getAttribute(key);
		const from = id === null ? undefined : before.get(id);
		if (!from) continue;

		const to = el.getBoundingClientRect();
		const dx = from.left - to.left;
		const dy = from.top - to.top;
		// Sub-pixel shifts are layout noise, not movement worth animating.
		if (Math.abs(dx) < 1 && Math.abs(dy) < 1) continue;

		moved++;
		el.animate([{ transform: `translate(${dx}px, ${dy}px)` }, { transform: 'none' }], {
			duration: FLIP_MS,
			easing: FLIP_EASING
		});
	}

	return moved;
}
