<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { Icon } from '$lib/icons';
	import { edgeScroll, isIdentity, moveIndex, slotAt } from '$lib/review/reorder';
	import { measure, play, type Positions } from '$lib/review/flip';
	import { imageUrl } from '$lib/hub/hub';
	import { portal } from '$lib/review/portal';
	import { uiZoom } from '$lib/zoom';
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
		current?: number;
		reorderable?: boolean;
		scanning?: boolean;
		layout?: 'strip' | 'overview';
		tile?: number;
		grid?: HTMLDivElement | null;
		onshow: (index: number) => void;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
		onreorder: (order: number[]) => void;
		onscanmore?: () => void;
		onexpand?: () => void;
	} = $props();

	const overview = $derived(layout === 'overview');

	// Touch must hold first so a swipe still scrolls the strip.
	const HOLD_MS = 250;
	const MOVE_PX = 6;

	let drag = $state<{
		from: number;
		over: number | null;
		grab: { x: number; y: number };
		size: { w: number; h: number };
	} | null>(null);
	let pressing = $state<number | null>(null);
	// Apart from `drag` so the grid is already in its final order while the ghost glides in.
	let settle = $state<{ page: PageDto; x: number; y: number; w: number; h: number } | null>(null);
	// Set a frame later so the lift transition actually plays.
	let lifted = $state(false);
	let pressTimer: ReturnType<typeof setTimeout> | null = null;
	let pressStart: { x: number; y: number; index: number; hold: boolean } | null = null;
	let rects: DOMRectReadOnly[] = [];
	let at = $state({ x: 0, y: 0 });
	let scrolling: number | null = null;
	// Taken when a drag begins: the ghost is styled in zoomed pixels, the pointer is not.
	let zoom = 1;
	let settleTimer: ReturnType<typeof setTimeout> | null = null;

	const SETTLE_MS = 200;

	const shown = $derived(
		drag && drag.over !== null
			? moveIndex(pages.length, drag.from, drag.over).map((i) => pages[i])
			: pages
	);

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

	// The ghost is held by `pressed`: a mouse drag starts only after moving, so `at` would jump.
	function begin(index: number, pointerId: number, pressed: { x: number; y: number }) {
		pressing = null;
		clearSettle();
		zoom = uiZoom();
		rects = slots().map((el) => el.getBoundingClientRect());
		const tile = rects[index] ?? new DOMRect(pressed.x, pressed.y, 0, 0);
		// From the current pointer, so a quick flick-and-release doesn't drop on the start slot.
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

	function hover(over: number | null) {
		if (!drag || drag.over === over) return;
		const from = measure(grid);
		drag = { ...drag, over };
		requestAnimationFrame(() => play(grid, from));
	}

	function autoScroll() {
		scrolling = null;
		if (!drag || !grid) return;
		const { dx, dy } = edgeScroll(grid.getBoundingClientRect(), at.x, at.y);
		if (dx || dy) {
			const { scrollLeft, scrollTop } = grid;
			grid.scrollBy?.({ left: dx, top: dy });
			// Shifted, not re-measured: mid-slide a tile's box is wherever its animation has it.
			const movedX = (grid.scrollLeft - scrollLeft) * zoom;
			const movedY = (grid.scrollTop - scrollTop) * zoom;
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
			// A flick can end without a final pointermove.
			if (e.type === 'pointerup') at = { x: e.clientX, y: e.clientY };
			const from = drag.from;
			const over = e.type === 'pointerup' ? (slotAt(rects, at.x, at.y) ?? drag.over) : from;
			const page = pages[from];
			const { w, h } = drag.size;
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

	let before: Positions | null = null;

	function reorderWithFlip(order: number[]) {
		before = measure(grid);
		onreorder(order);
	}

	$effect(() => {
		// Tracks the order; `before` is only set by a keyboard reorder.
		void pages.map((p) => p.Index).join();
		if (!before) return;
		const from = before;
		before = null;
		requestAnimationFrame(() => play(grid, from));
	});

	// A pending hold would otherwise start a drag on an unmounted grid.
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

	// The strip's width must match the save card's, or the stage is off-centre.
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
	const cell = $derived(overview ? 'w-full' : 'w-[70px] flex-none lg:w-full');
</script>

<div class={panel}>
	{#if onexpand && !overview}
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

	<!-- The pointer and Alt+Arrow handlers are delegated from the tiles' own buttons. -->
	<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
	<div
		class="{scroller} {drag ? 'cursor-grabbing touch-none select-none' : ''}"
		style={overview
			? `grid-template-columns: repeat(auto-fill, minmax(${tile}px, 1fr))`
			: undefined}
		role="list"
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
			<div
				class="{cell} animate-page-in transition-transform duration-200 ease-spring {held
					? 'rounded-[14px] bg-accent-50 outline-[1.5px] -outline-offset-[1.5px] outline-accent-dash outline-dashed'
					: ''} {pressing === page.Index ? 'scale-[0.95]' : ''}"
				role="listitem"
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

		<!-- `contents`: list items without a box of their own. -->
		{#if scanning}
			<div class="contents" role="listitem">
				<div
					class="{cell} rounded-[14px] bg-accent-50 shadow-ring-pending"
					style="aspect-ratio: 1 / 1.2"
					role="status"
					aria-label={t('review.scanningNext')}
				></div>
			</div>
		{/if}

		{#if onscanmore && !scanning}
			<div class="contents" role="listitem">
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
			</div>
		{/if}
	</div>
</div>

{#if ghost}
	<!-- Portalled: a transformed ancestor would otherwise pin a fixed element to itself.
	     Translate has no easing so it keeps up; the lift on the inner box eases. -->
	<div
		{@attach portal}
		class="pointer-events-none fixed top-0 left-0 z-[70] {settle
			? 'transition-transform duration-200 ease-spring'
			: ''}"
		style="width: {ghost.w / zoom}px; height: {ghost.h / zoom}px; transform: translate({ghost.x /
			zoom}px, {ghost.y / zoom}px)"
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
