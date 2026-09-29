<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { Icon } from '$lib/icons';
	import { edgeScroll, isIdentity, moveIndex, slotAt } from '$lib/review/reorder';
	import { measure, play, type Positions } from '$lib/review/flip';
	import { DEFAULT_TILE } from '$lib/review/overview';
	import PageThumb from './PageThumb.svelte';

	let {
		sessionId,
		pages,
		selection,
		current = -1,
		reorderable = true,
		scanning = false,
		documents,
		layout = 'strip',
		tile = DEFAULT_TILE,
		onshow,
		ontoggle,
		onopen,
		onreorder,
		onscanmore,
		onexpand
	}: {
		sessionId: string;
		pages: PageDto[];
		selection: ReadonlySet<number>;
		/** Index of the page the stage is showing, ringed in the strip. */
		current?: number;
		reorderable?: boolean;
		/** Shows a placeholder tile for the page currently coming off the feeder. */
		scanning?: boolean;
		/** Page index to document number, so a split shows where it falls. */
		documents?: Map<number, number>;
		/**
		 * `strip` is the filmstrip beside the stage; `overview` is the same pages as a
		 * wrapping grid, sized by `tile`, for the full-screen all-pages view.
		 */
		layout?: 'strip' | 'overview';
		/** Minimum width of one tile in the overview, i.e. the zoom level. */
		tile?: number;
		onshow: (index: number) => void;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
		onreorder: (order: number[]) => void;
		onscanmore?: () => void;
		/** Opens the full-screen overview; the strip shows a button for it when set. */
		onexpand?: () => void;
	} = $props();

	const overview = $derived(layout === 'overview');

	// A mouse drags as soon as it moves. A finger has to press and hold ~250 ms first,
	// so a swipe still scrolls the strip. Either way a quick tap stays a tap (show/select).
	const HOLD_MS = 250;
	const MOVE_PX = 6;

	let grid = $state<HTMLDivElement | null>(null);
	let drag = $state<{ from: number; over: number | null } | null>(null);
	let pressTimer: ReturnType<typeof setTimeout> | null = null;
	let pressStart: { x: number; y: number; index: number; hold: boolean } | null = null;
	let rects: DOMRectReadOnly[] = [];
	let at = { x: 0, y: 0 };
	let scrolling: number | null = null;

	function slots(): HTMLElement[] {
		return grid ? [...grid.querySelectorAll<HTMLElement>('[data-index]')] : [];
	}

	function begin(index: number, pointerId: number) {
		rects = slots().map((el) => el.getBoundingClientRect());
		// Straight from where the pointer already is: a quick flick and release would
		// otherwise drop on the slot the drag started in.
		drag = { from: index, over: slotAt(rects, at.x, at.y) ?? index };
		try {
			grid?.setPointerCapture(pointerId);
		} catch {
			// a synthetic pointer (tests) has no capture to take
		}
		scrolling ??= requestAnimationFrame(autoScroll);
	}

	/**
	 * Pulls the scroller when a drag hovers near its edge, so a page can be dropped
	 * somewhere that was off screen when the drag started.
	 */
	function autoScroll() {
		scrolling = null;
		if (!drag || !grid) return;
		const { dx, dy } = edgeScroll(grid.getBoundingClientRect(), at.x, at.y);
		if (dx || dy) {
			grid.scrollBy?.({ left: dx, top: dy });
			// The slots moved under the pointer, so where it now points has changed too.
			rects = slots().map((el) => el.getBoundingClientRect());
			drag = { ...drag, over: slotAt(rects, at.x, at.y) };
		}
		scrolling = requestAnimationFrame(autoScroll);
	}

	function pointerdown(e: PointerEvent) {
		if (!reorderable || e.button !== 0) return;
		const target = (e.target as HTMLElement).closest<HTMLElement>('[data-index]');
		if (!target) return;
		const index = Number(target.dataset.index);
		const hold = e.pointerType !== 'mouse';
		pressStart = { x: e.clientX, y: e.clientY, index, hold };
		at = { x: e.clientX, y: e.clientY };
		if (hold) pressTimer = setTimeout(() => begin(index, e.pointerId), HOLD_MS);
	}

	function pointermove(e: PointerEvent) {
		at = { x: e.clientX, y: e.clientY };
		if (drag) {
			drag = { ...drag, over: slotAt(rects, e.clientX, e.clientY) };
			e.preventDefault();
			return;
		}
		if (!pressStart) return;
		if (Math.hypot(e.clientX - pressStart.x, e.clientY - pressStart.y) <= MOVE_PX) return;
		// Past the slop: a held press has turned into a drag, an unheld touch into a scroll.
		if (pressStart.hold) cancelPress();
		else {
			const { index } = pressStart;
			cancelPress();
			begin(index, e.pointerId);
			e.preventDefault();
		}
	}

	function pointerup(e: PointerEvent) {
		if (drag) {
			// Where the pointer was released, not where it last reported a move: a quick
			// flick can end without one in between, and the drop should land where it looks.
			if (e.type === 'pointerup') at = { x: e.clientX, y: e.clientY };
			const from = drag.from;
			const over = slotAt(rects, at.x, at.y) ?? drag.over;
			drag = null;
			stopScrolling();
			try {
				grid?.releasePointerCapture(e.pointerId);
			} catch {
				// capture may already be gone
			}
			if (over !== null && over !== from) {
				const order = moveIndex(pages.length, from, over);
				if (!isIdentity(order)) reorderWithFlip(order);
			}
			// Suppress the click that follows the drag.
			suppressClick = true;
			setTimeout(() => (suppressClick = false), 0);
		}
		cancelPress();
	}

	function cancelPress() {
		if (pressTimer) clearTimeout(pressTimer);
		pressTimer = null;
		pressStart = null;
	}

	function stopScrolling() {
		if (scrolling !== null) cancelAnimationFrame(scrolling);
		scrolling = null;
	}

	let suppressClick = false;

	// Measured just before the order changes, replayed once the DOM has settled, so pages
	// slide to their new slots instead of jumping.
	let before: Positions | null = null;

	function reorderWithFlip(order: number[]) {
		before = measure(grid);
		onreorder(order);
	}

	$effect(() => {
		// Re-runs whenever the page order changes; `before` is only set by a reorder.
		void pages.map((p) => p.Index).join();
		if (!before) return;
		const from = before;
		before = null;
		requestAnimationFrame(() => play(grid, from));
	});

	$effect(() => stopScrolling);

	function show(index: number) {
		if (!suppressClick) onshow(index);
	}

	function toggle(index: number) {
		if (!suppressClick) ontoggle(index);
	}

	/** Keyboard reorder: Alt+Arrow moves the focused page, either axis. */
	function keydown(e: KeyboardEvent) {
		const back = e.key === 'ArrowLeft' || e.key === 'ArrowUp';
		const forward = e.key === 'ArrowRight' || e.key === 'ArrowDown';
		if (!reorderable || !e.altKey || (!back && !forward)) return;
		const target = (e.target as HTMLElement).closest<HTMLElement>('[data-index]');
		if (!target) return;
		const from = Number(target.dataset.index);
		const to = back ? from - 1 : from + 1;
		if (to < 0 || to >= pages.length) return;
		e.preventDefault();
		reorderWithFlip(moveIndex(pages.length, from, to));
		queueMicrotask(() => slots()[to]?.querySelector<HTMLElement>('button')?.focus());
	}

	// The strip is its own panel beside the stage; the overview just fills the sheet
	// its overlay has already drawn.
	const panel = $derived(
		overview
			? 'flex min-h-0 flex-1 flex-col'
			: 'flex w-full flex-col gap-2 rounded-[22px] bg-surface p-3 shadow-panel lg:h-full lg:w-[clamp(140px,11vw,190px)]'
	);
	const scroller = $derived(
		overview
			? 'grid min-h-0 flex-1 gap-3 overflow-y-auto overscroll-contain p-1 pb-6'
			: 'flex min-h-0 flex-1 flex-row gap-3 overflow-x-auto lg:flex-col lg:overflow-x-visible lg:overflow-y-auto'
	);
	/** One tile: a fixed sliver in the strip, a zoomable cell of the overview's grid. */
	const cell = $derived(overview ? 'w-full' : 'w-[70px] flex-none lg:w-full');
</script>

<div class={panel}>
	{#if onexpand && !overview}
		<!-- No page count here: the stage already says which page of how many. -->
		<button
			type="button"
			class="flex h-8 w-full flex-none items-center justify-center gap-1.5 rounded-full text-xs font-bold text-accent transition-colors hover:bg-accent-hover50"
			onclick={onexpand}
			data-testid="overview-open"
		>
			<Icon name="expand" size={14} />
			All pages
		</button>
	{/if}

	<div
		class="{scroller} {drag ? 'cursor-grabbing touch-none select-none' : ''}"
		style={overview
			? `grid-template-columns: repeat(auto-fill, minmax(${tile}px, 1fr))`
			: undefined}
		role="listbox"
		aria-multiselectable={overview}
		aria-label="Pages. Drag to reorder, or use Alt+Arrow keys."
		tabindex="-1"
		bind:this={grid}
		onpointerdown={pointerdown}
		onpointermove={pointermove}
		onpointerup={pointerup}
		onpointercancel={pointerup}
		onkeydown={keydown}
		ondragstart={(e) => e.preventDefault()}
		data-testid="page-grid"
		data-layout={layout}
		data-dragging={drag !== null}
	>
		{#each pages as page (page.Index)}
			<div
				class="{cell} animate-page-in transition-transform duration-500 ease-spring {drag?.from ===
				page.Index
					? 'opacity-35'
					: ''} {drag && drag.over === page.Index && drag.from !== page.Index
					? 'rounded-[14px] shadow-ring-outline'
					: ''}"
				role="option"
				aria-selected={overview ? selection.has(page.Index) : page.Index === current}
				data-index={page.Index}
				style="animation-delay: {Math.min(page.Index, 10) * 45}ms"
			>
				<PageThumb
					{sessionId}
					{page}
					selected={selection.has(page.Index)}
					current={page.Index === current}
					selectable={overview}
					document={page.StartsDocument && (documents?.get(page.Index) ?? 0) > 1
						? documents!.get(page.Index)!
						: null}
					onshow={show}
					ontoggle={toggle}
					{onopen}
				/>
			</div>
		{/each}

		{#if scanning}
			<div
				class="{cell} rounded-[14px] bg-accent-50 shadow-ring-pending"
				style="aspect-ratio: 1 / 1.2"
				aria-label="Scanning the next page"
			></div>
		{/if}

		{#if onscanmore && !scanning}
			<button
				type="button"
				onclick={onscanmore}
				data-testid="scan-more-tile"
				class="{cell} flex flex-col items-center justify-center gap-1 rounded-[14px] border-[1.5px] border-dashed border-accent-dash text-xs font-bold text-accent transition-colors hover:border-accent hover:bg-accent-hover50"
				style="aspect-ratio: 1 / 1.2"
			>
				<Icon name="plus" size={16} />
				Scan more
			</button>
		{/if}
	</div>
</div>
