import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/svelte';
import Icon from '$lib/icons/Icon.svelte';
import { DEFAULT_SIZE, DEFAULT_STROKE, icons, presetIcons, type IconName } from '$lib/icons/paths';

const names = Object.keys(icons) as IconName[];

describe('Icon', () => {
	it('draws every glyph with at least one shape', () => {
		for (const name of names) {
			const { container, unmount } = render(Icon, { name });
			const svg = container.querySelector('svg');
			expect(svg, name).not.toBeNull();
			expect(svg!.children.length, name).toBeGreaterThan(0);
			unmount();
		}
	});

	it('falls back to the default size and stroke', () => {
		const { container } = render(Icon, { name: 'plus' });
		const svg = container.querySelector('svg')!;
		expect(svg.getAttribute('width')).toBe(String(DEFAULT_SIZE));
		expect(svg.getAttribute('stroke-width')).toBe(String(DEFAULT_STROKE));
	});

	it('uses the size and stroke a glyph sets for itself', () => {
		const { container } = render(Icon, { name: 'scan' });
		const svg = container.querySelector('svg')!;
		expect(svg.getAttribute('width')).toBe('24');
		expect(svg.getAttribute('stroke-width')).toBe('2');
	});

	it('lets a caller override both', () => {
		const { container } = render(Icon, { name: 'scan', size: 32, stroke: 1 });
		const svg = container.querySelector('svg')!;
		expect(svg.getAttribute('width')).toBe('32');
		expect(svg.getAttribute('stroke-width')).toBe('1');
	});

	it('is decorative, so it stays out of the accessibility tree', () => {
		const { container } = render(Icon, { name: 'trash' });
		expect(container.querySelector('svg')!.getAttribute('aria-hidden')).toBe('true');
	});

	it('offers twelve preset glyphs that all exist', () => {
		expect(presetIcons).toHaveLength(12);
		for (const name of presetIcons) expect(icons).toHaveProperty(name);
	});
});
