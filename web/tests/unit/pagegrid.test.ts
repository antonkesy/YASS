import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/svelte';
import { createRawSnippet, tick } from 'svelte';
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

	it('shows the order a drop would leave, with the page in hand as a ghost, while dragging', async () => {
		vi.useFakeTimers();
		render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder: vi.fn()
		});
		layOut(tiles());

		await fireEvent.pointerDown(tiles()[0], { ...mouse, clientX: 10, clientY: 10 });
		await fireEvent.pointerMove(grid(), { ...mouse, clientX: 10, clientY: 220 });

		// The other pages have already made room: the held page's gap sits where it will land.
		expect([...tiles()].map((t) => t.dataset.index)).toEqual(['1', '2', '0']);
		expect(tiles()[2]).toHaveAttribute('data-held', 'true');
		// Picked up 10px in from the tile's corner, so it rides 10px behind the pointer.
		const ghost = () => screen.getByTestId('drag-ghost').getAttribute('style') ?? '';
		expect(ghost()).toContain('width: 80px');
		expect(ghost()).toContain('translate(0px, 210px)');

		// Dropped, the ghost glides into the gap and then goes.
		await fireEvent.pointerUp(grid(), { ...mouse, clientX: 10, clientY: 220 });
		expect(ghost()).toContain('translate(0px, 200px)');
		vi.advanceTimersByTime(250);
		await tick();
		expect(screen.queryByTestId('drag-ghost')).toBeNull();
	});

	it('sinks the tile a finger is holding, until the hold becomes a drag', async () => {
		vi.useFakeTimers();
		render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder: vi.fn()
		});
		layOut(tiles());

		await fireEvent.pointerDown(tiles()[1], { ...touch, clientX: 10, clientY: 110 });
		expect(tiles()[1].className).toContain('scale-[0.95]');

		vi.advanceTimersByTime(300);
		await tick();
		expect(tiles()[1].className).not.toContain('scale-[0.95]');
		expect(tiles()[1]).toHaveAttribute('data-held', 'true');
		expect(screen.getByTestId('drag-ghost')).toBeInTheDocument();

		await fireEvent.pointerUp(grid(), { ...touch, clientX: 10, clientY: 110 });
	});

	it('drops a hold still counting down when the strip goes away', async () => {
		vi.useFakeTimers();
		const { unmount } = render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder: vi.fn()
		});
		layOut(tiles());

		await fireEvent.pointerDown(tiles()[1], { ...touch, clientX: 10, clientY: 110 });
		expect(vi.getTimerCount()).toBeGreaterThan(0);
		unmount();
		expect(vi.getTimerCount()).toBe(0);
	});

	it('leaves ticking pages to the overview, which the strip only links to', () => {
		render(PageGrid, {
			sessionId: 's1',
			pages,
			selection: new Set<number>(),
			onshow: vi.fn(),
			ontoggle: vi.fn(),
			onopen: vi.fn(),
			onreorder: vi.fn()
		});
		expect(screen.queryAllByTestId('page-select')).toHaveLength(0);
		expect(grid()).toHaveAttribute('aria-multiselectable', 'false');
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

	it('ticks pages here, and stops offering select all once they all are', async () => {
		const props = open();
		expect(screen.getAllByTestId('page-select')).toHaveLength(pages.length);
		await fireEvent.click(screen.getAllByTestId('page-select')[1]);
		expect(props.ontoggle).toHaveBeenCalledWith(1);

		await fireEvent.click(screen.getByTestId('overview-select-all'));
		expect(props.onselectall).toHaveBeenCalled();

		open({ selection: new Set([0, 1, 2]) });
		expect(screen.getAllByTestId('overview-select-all')[1]).toBeDisabled();
	});

	it('shows the page-edit toolbar it is handed, so edits act without leaving', () => {
		open({
			children: createRawSnippet(() => ({
				render: () => '<div data-testid="stub-toolbar">tools</div>'
			}))
		});
		expect(screen.getByTestId('page-overview')).toContainElement(
			screen.getByTestId('stub-toolbar')
		);
	});

	it('closes on Escape and on the close button', async () => {
		const props = open();
		await fireEvent.keyDown(screen.getByTestId('page-overview'), { key: 'Escape' });
		expect(props.onclose).toHaveBeenCalledTimes(1);

		await fireEvent.click(screen.getByTestId('overview-close'));
		expect(props.onclose).toHaveBeenCalledTimes(2);
	});
});
