import { describe, expect, it } from 'vitest';
import { clear, prune, toggle } from '$lib/review/selection';

describe('selection', () => {
	it('toggles without mutating the input', () => {
		const a = clear();
		const b = toggle(a, 2);
		const c = toggle(b, 2);
		expect(a.size).toBe(0);
		expect([...b]).toEqual([2]);
		expect(c.size).toBe(0);
	});

	it('prunes indices outside the page count', () => {
		expect([...prune(new Set([0, 3, 7]), 4)]).toEqual([0, 3]);
	});
});
