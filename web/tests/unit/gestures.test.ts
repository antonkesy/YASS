import { describe, expect, it } from 'vitest';
import { detectSwipe, step } from '$lib/review/swipe';
import { isIdentity, moveIndex, slotAt } from '$lib/review/reorder';

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
		expect(slotAt(rects, 15, 5)).toBeNull();
	});
});
