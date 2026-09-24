/** Horizontal swipe detection from pointer events; vertical movement cancels so scrolling still works. */
export type SwipeDirection = 'left' | 'right';

export function detectSwipe(dx: number, dy: number, threshold = 50): SwipeDirection | null {
	if (Math.abs(dx) < threshold || Math.abs(dy) > Math.abs(dx)) return null;
	return dx < 0 ? 'left' : 'right';
}

/** Index after navigating; clamps at the ends instead of wrapping so a swipe past the last page is a no-op. */
export function step(index: number, count: number, direction: SwipeDirection): number {
	const next = direction === 'left' ? index + 1 : index - 1;
	return Math.min(Math.max(next, 0), Math.max(count - 1, 0));
}
