/** Zoom steps: minimum page tile width in px. */
export const TILE_SIZES = [80, 110, 150, 210, 290] as const;

export const DEFAULT_TILE = 150;

export function tileStep(tile: number): number {
	let best = 0;
	for (let i = 1; i < TILE_SIZES.length; i++)
		if (Math.abs(TILE_SIZES[i] - tile) < Math.abs(TILE_SIZES[best] - tile)) best = i;
	return best;
}

/** Skips steps within 10% of the current size, so a press from a fitted size is never a no-op. */
export function zoomTile(tile: number, delta: number): number {
	if (delta > 0)
		return TILE_SIZES.find((size) => size >= tile * 1.1) ?? Math.max(tile, DEFAULT_TILE);
	if (delta < 0)
		return [...TILE_SIZES].reverse().find((size) => size <= tile * 0.9) ?? TILE_SIZES[0];
	return TILE_SIZES[tileStep(tile)];
}

/**
 * Tiles stretch to fill their row (`minmax(tile, 1fr)`), so keeps stepping until the number
 * of tiles per row actually changes.
 */
export function zoomGrid(tile: number, delta: number, width: number, gap: number): number {
	const perRow = (t: number) => Math.max(1, Math.floor((width + gap) / (t + gap)));
	const now = perRow(tile);
	let next = zoomTile(tile, delta);
	while (delta < 0 ? perRow(next) <= now : perRow(next) >= now) {
		const further = zoomTile(next, delta);
		if (further === next) break;
		next = further;
	}
	return next;
}

/** Tile height is modelled as `slope × width + intercept`; the caller measures both. */
export function fitTile(options: {
	count: number;
	width: number;
	height: number;
	gap: number;
	slope: number;
	intercept: number;
}): number {
	const { count, width, height, gap, slope, intercept } = options;
	if (count <= 0 || width <= 0 || height <= 0 || slope <= 0) return DEFAULT_TILE;

	// Fewer columns = wider tiles, so the first column count that fits vertically is the widest.
	for (let columns = 1; ; columns++) {
		const tile = (width - (columns - 1) * gap) / columns;
		if (tile < TILE_SIZES[0]) break;

		const rows = Math.ceil(count / columns);
		if (rows * (slope * tile + intercept) + (rows - 1) * gap <= height) return Math.floor(tile);
	}

	return TILE_SIZES[0];
}
