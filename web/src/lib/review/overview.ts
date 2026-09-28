/**
 * The overview's zoom steps, as the minimum width of one page tile in pixels. Small
 * enough at the bottom to hold a long scan on one screen, big enough at the top to
 * read a heading off a thumbnail.
 */
export const TILE_SIZES = [80, 110, 150, 210, 290] as const;

export const DEFAULT_TILE = 150;

/** The nearest step to `tile`, so a stored size still lines up after the steps change. */
export function tileStep(tile: number): number {
	let best = 0;
	for (let i = 1; i < TILE_SIZES.length; i++)
		if (Math.abs(TILE_SIZES[i] - tile) < Math.abs(TILE_SIZES[best] - tile)) best = i;
	return best;
}

/** Zooms in (`delta` 1) or out (-1) from whatever size is showing, stopping at the ends. */
export function zoomTile(tile: number, delta: number): number {
	const at = tileStep(tile) + delta;
	return TILE_SIZES[Math.min(TILE_SIZES.length - 1, Math.max(0, at))];
}
