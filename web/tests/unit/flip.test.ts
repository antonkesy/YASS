import { afterEach, describe, expect, it, vi } from 'vitest';
import { FLIP_MS, measure, play } from '$lib/review/flip';

function grid(rects: Record<string, [number, number]>) {
	const animate = vi.fn();
	const elements = Object.entries(rects).map(([index, [left, top]]) => ({
		getAttribute: (name: string) => (name === 'data-index' ? index : null),
		getBoundingClientRect: () => ({ left, top }) as DOMRect,
		animate
	}));
	return {
		container: {
			querySelectorAll: () => elements,
			animate: () => undefined
		} as unknown as HTMLElement,
		animate
	};
}

describe('flip', () => {
	afterEach(() => vi.unstubAllGlobals());

	it('measures every keyed element', () => {
		const { container } = grid({ '0': [0, 0], '1': [0, 100] });
		const positions = measure(container);
		expect([...positions.keys()]).toEqual(['0', '1']);
		expect(positions.get('1')?.top).toBe(100);
	});

	it('animates from the old position to the new one', () => {
		const first = grid({ '0': [0, 0], '1': [0, 100] });
		const before = measure(first.container);

		const second = grid({ '0': [0, 100], '1': [0, 0] });
		const moved = play(second.container, before);

		expect(moved).toBe(2);
		const [keyframes, options] = second.animate.mock.calls[0];
		expect(keyframes[0].transform).toBe('translate(0px, -100px)');
		expect(keyframes[1].transform).toBe('none');
		expect(options.duration).toBe(FLIP_MS);
	});

	it('moves in the zoomed UI’s own pixels', () => {
		document.documentElement.style.setProperty('--ui-zoom', '2');
		try {
			const before = measure(grid({ '0': [0, 0] }).container);
			const after = grid({ '0': [0, 100] });
			play(after.container, before);

			expect(after.animate.mock.calls[0][0][0].transform).toBe('translate(0px, -50px)');
		} finally {
			document.documentElement.style.removeProperty('--ui-zoom');
		}
	});

	it('leaves alone anything that did not move', () => {
		const same = grid({ '0': [0, 0], '1': [0, 100] });
		const before = measure(same.container);
		const after = grid({ '0': [0, 0], '1': [0, 100] });

		expect(play(after.container, before)).toBe(0);
		expect(after.animate).not.toHaveBeenCalled();
	});

	it('ignores sub-pixel layout noise', () => {
		const first = grid({ '0': [0, 0] });
		const before = measure(first.container);
		const after = grid({ '0': [0.4, 0.2] });

		expect(play(after.container, before)).toBe(0);
	});

	it('still counts the moves but animates none of them under reduced motion', () => {
		vi.stubGlobal('matchMedia', (query: string) => ({ matches: query.includes('reduce') }));
		const before = measure(grid({ '0': [0, 0] }).container);
		const after = grid({ '0': [0, 100] });

		expect(play(after.container, before)).toBe(1);
		expect(after.animate).not.toHaveBeenCalled();
	});

	it('does nothing without a container', () => {
		expect(measure(null).size).toBe(0);
		expect(play(null, new Map())).toBe(0);
	});
});
