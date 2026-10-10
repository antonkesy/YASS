<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { Icon } from '$lib/icons';
	import { edgeScroll, isIdentity, moveIndex, slotAt } from '$lib/review/reorder';
	import { measure, play, type Positions } from '$lib/review/flip';
	import { imageUrl } from '$lib/hub/hub';
	import { portal } from '$lib/review/portal';
	import { DEFAULT_TILE } from '$lib/review/overview';
	import PageThumb from './PageThumb.svelte';

	let {
		sessionId,
		pages,
		selection,
		current = -1,
		reorderable = true,
		scanning = false,
		layout = 'strip',
		tile = DEFAULT_TILE,
		grid = $bindable(null),
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
		/**
		 * `strip` is the filmstrip beside the stage; `overview` is the same pages as a
		 * wrapping grid, sized by `tile`, for the full-screen all-pages view.
		 */
		layout?: 'strip' | 'overview';
		/** Minimum width of one tile in the overview, i.e. the zoom level. */
		tile?: number;
		/** The scrolling element, out here so the overview can ask whether everything fits. */
		grid?: HTMLDivElement | null;
		onshow: (index: number) => void;
		ontoggle: (index: number) => void;
		/** Opens a page full screen, offered per tile in the overview and by the toolbar in the strip. */
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

	/**
	 * A drag in flight. `over` is the slot (a position in the grid, not a page) the page would
	 * land in; `grab` is where inside the tile it was picked up, so the ghost stays under the
	 * finger; `size` is the tile's, so the ghost is the tile and not a guess at it.
	 */
	let drag = $state<{
		from: number;
		over: number | null;
		grab: { x: number; y: number };
		size: { w: number; h: number };
	} | null>(null);
	/** The page a finger is holding down on, before the hold has become a drag. */
	let pressing = $state<number | null>(null);
	/**
	 * The ghost after a drop, gliding into the slot the page landed in. Kept apart from
	 * `drag` so the grid can already be in its final order while the ghost catches up.
	 */
	let settle = $state<{ page: PageDto; x: number; y: number; w: number; h: number } | null>(null);
	/** Lifts the ghost a frame after it appears, so the lift is seen rather than just there. */
	let lifted = $state(false);
	let pressTimer: ReturnType<typeof setTimeout> | null = null;
	let pressStart: { x: number; y: number; index: number; hold: boolean } | null = null;
	let rects: DOMRectReadOnly[] = [];
	let at = $state({ x: 0, y: 0 });
	let scrolling: number | null = null;
	let settleTimer: ReturnType<typeof setTimeout> | null = null;

	const SETTLE_MS = 200;

	/**
	 * The pages in the order the grid shows them: while a drag is on, the order it would
	 * leave behind, so the other pages make room where the held one is going to land. That
	 * moving gap, more than any outline, is what says where a drop will put it.
	 */
	const shown = $derived(
		drag && drag.over !== null
			? moveIndex(pages.length, drag.from, drag.over).map((i) => pages[i])
			: pages
	);

	/** The page under the pointer as a floating card, while held and while settling. */
	const ghost = $derived(
		drag
			? {
					page: pages[drag.from],
					x: at.x - drag.grab.x,
					y: at.y - drag.grab.y,
					w: drag.size.w,
					h: drag.size.h
				}
			: settle
	);

	function slots(): HTMLElement[] {
		return grid ? [...grid.querySelectorAll<HTMLElement>('[data-index]')] : [];
	}

	/**
	 * Starts a drag of the page at `index`. `pressed` is where the tile was first pressed,
	 * which is where the ghost is held by: a mouse only becomes a drag once it has moved,
	 * and measured from there the ghost would jump by that first move.
	 */
	function begin(index: number, pointerId: number, pressed: { x: number; y: number }) {
		pressing = null;
		clearSettle();
		rects = slots().map((el) => el.getBoundingClientRect());
		const tile = rects[index] ?? new DOMRect(pressed.x, pressed.y, 0, 0);
		// Straight from where the pointer already is: a quick flick and release would
		// otherwise drop on the slot the drag started in.
		drag = {
			from: index,
			over: slotAt(rects, at.x, at.y) ?? index,
			grab: { x: pressed.x - tile.left, y: pressed.y - tile.top },
			size: { w: tile.width, h: tile.height }
		};
		lifted = false;
		requestAnimationFrame(() => (lifted = drag !== null));
		try {
			grid?.setPointerCapture(pointerId);
		} catch {
			// a synthetic pointer (tests) has no capture to take
		}
		scrolling ??= requestAnimationFrame(autoScroll);
	}

	/**
	 * Moves the held page's gap to another slot, sliding the pages in between out of the
	 * way. Measured before and played after, like a real reorder, so they travel rather
	 * than jump.
	 */
	function hover(over: number | null) {
		if (!drag || drag.over === over) return;
		const from = measure(grid);
		drag = { ...drag, over };
		requestAnimationFrame(() => play(grid, from));
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
			const { scrollLeft, scrollTop } = grid;
			grid.scrollBy?.({ left: dx, top: dy });
			// The slots moved under the pointer by however far the scroller actually went.
			// Shifted rather than re-measured: mid-slide, a tile's box is wherever its
			// animation has it, not where its slot is.
			const movedX = grid.scrollLeft - scrollLeft;
			const movedY = grid.scrollTop - scrollTop;
			if (movedX || movedY) {
				rects = rects.map((r) => new DOMRect(r.x - movedX, r.y - movedY, r.width, r.height));
				hover(slotAt(rects, at.x, at.y));
			}
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
		if (hold) {
			// The tile sinks under the finger while the hold counts down, then lifts: the
			// press is seen to be doing something before it has become a drag.
			pressing = index;
			const pressed = { x: e.clientX, y: e.clientY };
			pressTimer = setTimeout(() => begin(index, e.pointerId, pressed), HOLD_MS);
		}
	}

	function pointermove(e: PointerEvent) {
		at = { x: e.clientX, y: e.clientY };
		if (drag) {
			hover(slotAt(rects, e.clientX, e.clientY));
			e.preventDefault();
			return;
		}
		if (!pressStart) return;
		if (Math.hypot(e.clientX - pressStart.x, e.clientY - pressStart.y) <= MOVE_PX) return;
		// Past the slop: a held press has turned into a drag, an unheld touch into a scroll.
		if (pressStart.hold) cancelPress();
		else {
			const { index, x, y } = pressStart;
			cancelPress();
			begin(index, e.pointerId, { x, y });
			e.preventDefault();
		}
	}

	function pointerup(e: PointerEvent) {
		if (drag) {
			// Where the pointer was released, not where it last reported a move: a quick
			// flick can end without one in between, and the drop should land where it looks.
			if (e.type === 'pointerup') at = { x: e.clientX, y: e.clientY };
			const from = drag.from;
			const over = e.type === 'pointerup' ? (slotAt(rects, at.x, at.y) ?? drag.over) : from;
			const page = pages[from];
			const { w, h } = drag.size;
			// The grid is already laid out the way the drop leaves it (or, when the drag is
			// going nowhere, slides back), so the ghost glides into the gap and nothing else
			// has to move.
			const held = measure(grid);
			drag = null;
			stopScrolling();
			try {
				grid?.releasePointerCapture(e.pointerId);
			} catch {
				// capture may already be gone
			}
			const order = over !== null && over !== from ? moveIndex(pages.length, from, over) : null;
			if (order && !isIdentity(order)) {
				onreorder(order);
			} else {
				requestAnimationFrame(() => play(grid, held));
			}
			const slot = rects[over ?? from];
			if (slot) {
				settle = { page, x: slot.left, y: slot.top, w, h };
				lifted = false;
				settleTimer = setTimeout(clearSettle, SETTLE_MS);
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
		pressing = null;
	}

	function clearSettle() {
		if (settleTimer) clearTimeout(settleTimer);
		settleTimer = null;
		settle = null;
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

	// A hold still counting down when the strip goes would otherwise start a drag on a
	// grid that is no longer there.
	$effect(() => () => {
		cancelPress();
		stopScrolling();
		clearSettle();
	});

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
	//
	// Its width is kept close to the save card's on the review screen, because the stage
	// sits between the two: the page on screen is only centred when the columns either
	// side of it match. Change one and the other wants changing too.
	const panel = $derived(
		overview
			? 'flex min-h-0 flex-1 flex-col'
			: 'flex w-full flex-col gap-2 rounded-[22px] bg-surface p-3 shadow-panel lg:h-full lg:w-(--review-strip-w)'
	);
	const scroller = $derived(
		overview
			? 'grid min-h-0 flex-1 content-start gap-3 overflow-y-auto overscroll-contain p-1 pb-6'
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
			{t('review.allPages')}
		</button>
	{/if}

	<div
		class="{scroller} {drag ? 'cursor-grabbing touch-none select-none' : ''}"
		style={overview
			? `grid-template-columns: repeat(auto-fill, minmax(${tile}px, 1fr))`
			: undefined}
		role="listbox"
		aria-multiselectable={overview}
		aria-label={t('review.gridLabel')}
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
		{#each shown as page (page.Index)}
			{@const held = drag?.from === page.Index}
			<!-- The held page's own tile is the gap it will drop into: a dashed slot that
			     travels with the pointer while the page itself rides along as the ghost. -->
			<div
				class="{cell} animate-page-in transition-transform duration-200 ease-spring {held
					? 'rounded-[14px] bg-accent-50 outline-[1.5px] -outline-offset-[1.5px] outline-accent-dash outline-dashed'
					: ''} {pressing === page.Index ? 'scale-[0.95]' : ''}"
				role="option"
				aria-selected={overview ? selection.has(page.Index) : page.Index === current}
				data-index={page.Index}
				data-held={held}
				style="animation-delay: {Math.min(page.Index, 10) * 45}ms"
			>
				<div class={held ? 'invisible' : ''}>
					<PageThumb
						{sessionId}
						{page}
						selected={selection.has(page.Index)}
						current={page.Index === current}
						selectable={overview}
						onshow={show}
						ontoggle={toggle}
						onopen={overview ? onopen : undefined}
					/>
				</div>
			</div>
		{/each}

		{#if scanning}
			<div
				class="{cell} rounded-[14px] bg-accent-50 shadow-ring-pending"
				style="aspect-ratio: 1 / 1.2"
				aria-label={t('review.scanningNext')}
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
				{t('review.scanMore')}
			</button>
		{/if}
	</div>
</div>

{#if ghost}
	<!--
		The page in hand. Fixed to the viewport and portalled out, because any transformed
		ancestor (the overlay's slide-in, the page's fade) would otherwise pin it to itself.
		Translate rides on the outer box with no easing, so it keeps up with the pointer;
		the lift lives on the inner one and eases, so picking up and putting down are seen.
	-->
	<div
		use:portal
		class="pointer-events-none fixed top-0 left-0 z-[70] {settle
			? 'transition-transform duration-200 ease-spring'
			: ''}"
		style="width: {ghost.w}px; height: {ghost.h}px; transform: translate({ghost.x}px, {ghost.y}px)"
		aria-hidden="true"
		data-testid="drag-ghost"
		data-lifted={lifted}
	>
		<div
			class="h-full w-full rounded-[14px] bg-surface p-1.5 transition-[transform,box-shadow] duration-200 ease-spring {lifted
				? 'scale-[1.06] rotate-2 shadow-modal'
				: 'shadow-panel'}"
		>
			<div
				class="flex w-full items-center justify-center overflow-hidden rounded-[6px] bg-surface shadow-page"
				style="aspect-ratio: 1 / 1.414"
			>
				{#if ghost.page.ThumbnailReady}
					<img
						src={imageUrl(sessionId, ghost.page.Index, ghost.page.Hash, 'thumb')}
						alt=""
						class="h-full w-full object-contain {ghost.page.Deleted ? 'opacity-50' : ''}"
						draggable="false"
					/>
				{/if}
			</div>
		</div>
	</div>
{/if}
