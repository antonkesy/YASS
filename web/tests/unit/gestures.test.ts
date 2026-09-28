import { describe, expect, it } from 'vitest';
import { detectSwipe, step } from '$lib/review/swipe';
import { EDGE_SPEED, edgeScroll, isIdentity, moveIndex, slotAt } from '$lib/review/reorder';
import { DEFAULT_TILE, TILE_SIZES, tileStep, zoomTile } from '$lib/review/overview';

describe('swipe', () => {
	it('needs a mostly horizontal move past the threshold', () => {
		expect(detectSwipe(-80, 10)).toBe('left');
		expect(detectSwipe(80, -10)).toBe('right');
		expect(detectSwipe(30, 0)).toBeNull();
		expect(detectSwipe(80, 90)).toBeNull();
	});

	it('steps within bounds', () => {
		expect(step(0, 3, 'left')).toBe(1);
		expect(step(2, 3, 'left')).toBe(2);
		expect(step(0, 3, 'right')).toBe(0);
		expect(step(0, 0, 'left')).toBe(0);
	});
});

describe('reorder', () => {
	it('moves an item and yields a permutation', () => {
		expect(moveIndex(4, 0, 2)).toEqual([1, 2, 0, 3]);
		expect(moveIndex(4, 3, 0)).toEqual([3, 0, 1, 2]);
		expect(isIdentity(moveIndex(4, 1, 1))).toBe(true);
		expect(isIdentity(moveIndex(4, 9, 1))).toBe(true);
	});

	it('finds the slot under a point', () => {
		const rects = [new DOMRect(0, 0, 10, 10), new DOMRect(20, 0, 10, 10)];
		expect(slotAt(rects, 5, 5)).toBe(0);
		expect(slotAt(rects, 25, 5)).toBe(1);
	});

	it('snaps a drop in a gap, or past the end, to the nearest slot', () => {
		const rects = [new DOMRect(0, 0, 10, 10), new DOMRect(20, 0, 10, 10)];
		expect(slotAt(rects, 13, 5)).toBe(0);
		expect(slotAt(rects, 18, 5)).toBe(1);
		expect(slotAt(rects, 500, 500)).toBe(1);
		expect(slotAt([], 5, 5)).toBeNull();
	});
});

describe('edge scroll', () => {
	const box = { left: 0, right: 200, top: 0, bottom: 400 };

	it('pulls the scroller only near an edge, and hardest at it', () => {
		expect(edgeScroll(box, 100, 200)).toEqual({ dx: 0, dy: 0 });
		expect(edgeScroll(box, 100, 0).dy).toBe(-EDGE_SPEED);
		expect(edgeScroll(box, 100, 400).dy).toBe(EDGE_SPEED);
		expect(edgeScroll(box, 0, 200).dx).toBe(-EDGE_SPEED);
		expect(Math.abs(edgeScroll(box, 100, 36).dy)).toBeLessThan(EDGE_SPEED);
	});

	it('stays still for a box with no size, which is a layout that has not happened', () => {
		expect(edgeScroll({ left: 0, right: 0, top: 0, bottom: 0 }, 0, 0)).toEqual({ dx: 0, dy: 0 });
	});
});

describe('overview zoom', () => {
	it('steps through the tile sizes and stops at the ends', () => {
		expect(zoomTile(DEFAULT_TILE, 1)).toBe(TILE_SIZES[tileStep(DEFAULT_TILE) + 1]);
		expect(zoomTile(TILE_SIZES[0], -1)).toBe(TILE_SIZES[0]);
		expect(zoomTile(TILE_SIZES[TILE_SIZES.length - 1], 1)).toBe(TILE_SIZES[TILE_SIZES.length - 1]);
	});

	it('snaps a stored size that is no longer a step to the nearest one', () => {
		expect(zoomTile(TILE_SIZES[2] + 3, 0)).toBe(TILE_SIZES[2]);
	});
});
