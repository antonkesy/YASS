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

/**
 * Zooms in (`delta` 1) or out (-1) from whatever size is showing, stopping at the ends.
 *
 * Measured from the size on screen rather than from the nearest step to it, because the size
 * on screen need not be a step at all: a first open fits the scan to the screen. And it skips
 * any step within a tenth of where it already is, so that a press always looks like one — a
 * fit of 297 stepping to 290 is a button that appears not to work.
 */
export function zoomTile(tile: number, delta: number): number {
	if (delta > 0)
		return TILE_SIZES.find((size) => size >= tile * 1.1) ?? Math.max(tile, DEFAULT_TILE);
	if (delta < 0)
		return [...TILE_SIZES].reverse().find((size) => size <= tile * 0.9) ?? TILE_SIZES[0];
	return TILE_SIZES[tileStep(tile)];
}

/**
 * The widest tile at which `count` of them still fit in one screen, for the size the
 * all-pages view opens at. Not one of {@link TILE_SIZES}: with a handful of pages the
 * biggest step still leaves half the screen empty, and the point of the first open is to
 * show the scan as large as it goes while keeping all of it in view.
 *
 * A tile's height is `slope × width + intercept` — the thumbnail scales, the row of
 * controls under it does not — and the caller measures both off two rendered sizes rather
 * than either of us guessing at the markup.
 */
export function fitTile(options: {
	count: number;
	/** Content box of the scrolling grid. */
	width: number;
	height: number;
	/** Gap between tiles, both ways. */
	gap: number;
	slope: number;
	intercept: number;
}): number {
	const { count, width, height, gap, slope, intercept } = options;
	if (count <= 0 || width <= 0 || height <= 0 || slope <= 0) return DEFAULT_TILE;

	// Fewer columns means wider tiles and more rows, so the first column count that fits
	// vertically is the widest one that fits at all. Not capped at the number of pages: a
	// scan of three that will not fit three abreast fits three of four columns instead,
	// which is what a narrower tile in the same grid means.
	for (let columns = 1; ; columns++) {
		const tile = (width - (columns - 1) * gap) / columns;
		if (tile < TILE_SIZES[0]) break;

		const rows = Math.ceil(count / columns);
		if (rows * (slope * tile + intercept) + (rows - 1) * gap <= height) return Math.floor(tile);
	}

	return TILE_SIZES[0];
}
