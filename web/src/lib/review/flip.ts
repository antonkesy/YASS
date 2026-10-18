import { play as animate } from '$lib/motion';
import { uiZoom } from '$lib/zoom';

export const FLIP_MS = 600;

export type Positions = Map<string, DOMRect>;

export function measure(container: HTMLElement | null, key = 'data-index'): Positions {
	const positions: Positions = new Map();
	if (!container) return positions;
	for (const el of container.querySelectorAll<HTMLElement>(`[${key}]`)) {
		const id = el.getAttribute(key);
		if (id !== null) positions.set(id, el.getBoundingClientRect());
	}
	return positions;
}

/** Returns how many elements moved, so callers can tell a real reorder from a no-op. */
export function play(container: HTMLElement | null, before: Positions, key = 'data-index'): number {
	if (!container || typeof container.animate !== 'function') return 0;

	let moved = 0;
	const zoom = uiZoom();
	for (const el of container.querySelectorAll<HTMLElement>(`[${key}]`)) {
		const id = el.getAttribute(key);
		const from = id === null ? undefined : before.get(id);
		if (!from) continue;

		const to = el.getBoundingClientRect();
		const dx = from.left - to.left;
		const dy = from.top - to.top;
		if (Math.abs(dx) < 1 && Math.abs(dy) < 1) continue;

		moved++;
		animate(
			el,
			[{ transform: `translate(${dx / zoom}px, ${dy / zoom}px)` }, { transform: 'none' }],
			FLIP_MS
		);
	}

	return moved;
}
