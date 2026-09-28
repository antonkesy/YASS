import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { tick } from 'svelte';
import PageGrid from '$lib/components/PageGrid.svelte';
import PageOverview from '$lib/components/PageOverview.svelte';
import { TILE_SIZES, zoomTile } from '$lib/review/overview';
import { page } from './fixtures';

const pages = [0, 1, 2].map((i) => page(i));

/**
 * jsdom lays nothing out, so the drop targets have to be told where they are: one
 * 90px-tall tile per row, as the filmstrip stacks them on a desktop.
 */
function layOut(tiles: NodeListOf<HTMLElement> | HTMLElement[]) {
	[...tiles].forEach((tile, i) => {
		tile.getBoundingClientRect = () => new DOMRect(0, i * 100, 80, 90);
	});
}

function grid() {
	return screen.getByTestId('page-grid');
}

function tiles() {
	return grid().querySelectorAll<HTMLElement>('[data-index]');
}

const mouse = { pointerType: 'mouse', pointerId: 1, button: 0 };
const touch = { pointerType: 'touch', pointerId: 2, button: 0 };

afterEach(() => vi.useRealTimers());

describe('PageGrid drag reorder', () => {
	it('drags with a mouse as soon as it moves, and reorders on drop', async () => {
		const onreorder = vi.fn();
		render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder
		});
		layOut(tiles());

		await fireEvent.pointerDown(tiles()[0], { ...mouse, clientX: 10, clientY: 10 });
		expect(grid()).toHaveAttribute('data-dragging', 'false');

		await fireEvent.pointerMove(grid(), { ...mouse, clientX: 10, clientY: 120 });
		expect(grid()).toHaveAttribute('data-dragging', 'true');

		await fireEvent.pointerUp(grid(), { ...mouse, clientX: 10, clientY: 120 });
		expect(onreorder).toHaveBeenCalledWith([1, 0, 2]);
	});

	it('drops onto the nearest page when the pointer lands past the last one', async () => {
		const onreorder = vi.fn();
		render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder
		});
		layOut(tiles());

		await fireEvent.pointerDown(tiles()[0], { ...mouse, clientX: 10, clientY: 10 });
		await fireEvent.pointerMove(grid(), { ...mouse, clientX: 10, clientY: 900 });
		await fireEvent.pointerUp(grid(), { ...mouse, clientX: 10, clientY: 900 });
		expect(onreorder).toHaveBeenCalledWith([1, 2, 0]);
	});

	it('lets a finger scroll the strip, and only reorders after a hold', async () => {
		vi.useFakeTimers();
		const onreorder = vi.fn();
		render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder
		});
		layOut(tiles());

		await fireEvent.pointerDown(tiles()[0], { ...touch, clientX: 10, clientY: 10 });
		await fireEvent.pointerMove(grid(), { ...touch, clientX: 10, clientY: 120 });
		expect(grid()).toHaveAttribute('data-dragging', 'false');
		await fireEvent.pointerUp(grid(), { ...touch, clientX: 10, clientY: 120 });
		expect(onreorder).not.toHaveBeenCalled();

		await fireEvent.pointerDown(tiles()[0], { ...touch, clientX: 10, clientY: 10 });
		vi.advanceTimersByTime(300);
		await tick();
		expect(grid()).toHaveAttribute('data-dragging', 'true');

		await fireEvent.pointerMove(grid(), { ...touch, clientX: 10, clientY: 120 });
		await fireEvent.pointerUp(grid(), { ...touch, clientX: 10, clientY: 120 });
		expect(onreorder).toHaveBeenCalledWith([1, 0, 2]);
	});

	it('offers the overview only when the caller can open one', async () => {
		const onexpand = vi.fn();
		const props = {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder: vi.fn()
		};
		const { rerender } = render(PageGrid, props);
		expect(screen.queryByTestId('overview-open')).toBeNull();

		await rerender({ ...props, onexpand });
		await fireEvent.click(screen.getByTestId('overview-open'));
		expect(onexpand).toHaveBeenCalled();
	});
});

describe('PageOverview', () => {
	const open = (over: Record<string, unknown> = {}) => {
		const props = {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			tile: TILE_SIZES[2],
			onzoom: vi.fn(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder: vi.fn(),
			onselectall: vi.fn(),
			onclear: vi.fn(),
			onclose: vi.fn(),
			...over
		};
		render(PageOverview, props);
		return props;
	};

	it('lays the pages out at the tile size it is given', () => {
		open();
		expect(grid()).toHaveAttribute('data-layout', 'overview');
		expect(grid().getAttribute('style')).toContain(`minmax(${TILE_SIZES[2]}px, 1fr)`);
	});

	it('zooms in and out a step at a time, stopping at the ends', async () => {
		const props = open();
		await fireEvent.click(screen.getByTestId('overview-zoom-in'));
		expect(props.onzoom).toHaveBeenCalledWith(zoomTile(TILE_SIZES[2], 1));

		await fireEvent.click(screen.getByTestId('overview-zoom-out'));
		expect(props.onzoom).toHaveBeenLastCalledWith(zoomTile(TILE_SIZES[2], -1));

		open({ tile: TILE_SIZES[TILE_SIZES.length - 1] });
		expect(screen.getAllByTestId('overview-zoom-in')[1]).toBeDisabled();
	});

	it('selects every page, then clears once they all are', async () => {
		const props = open();
		await fireEvent.click(screen.getByTestId('overview-select-all'));
		expect(props.onselectall).toHaveBeenCalled();

		const all = open({ selection: new Set([0, 1, 2]) });
		await fireEvent.click(screen.getAllByTestId('overview-select-all')[1]);
		expect(all.onclear).toHaveBeenCalled();
	});

	it('closes on Escape and on the close button', async () => {
		const props = open();
		await fireEvent.keyDown(screen.getByTestId('page-overview'), { key: 'Escape' });
		expect(props.onclose).toHaveBeenCalledTimes(1);

		await fireEvent.click(screen.getByTestId('overview-close'));
		expect(props.onclose).toHaveBeenCalledTimes(2);
	});
});
