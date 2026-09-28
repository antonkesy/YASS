import { describe, expect, it } from 'vitest';
import { clear, prune, retain, toggle } from '$lib/review/selection';

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

describe('selection over ids', () => {
	it('toggles and retains string members the same way', () => {
		const picked = toggle(toggle(clear<string>(), 'a'), 'b');
		expect([...picked]).toEqual(['a', 'b']);
		expect([...toggle(picked, 'a')]).toEqual(['b']);
		expect([...retain(picked, new Set(['b', 'c']))]).toEqual(['b']);
	});
});
