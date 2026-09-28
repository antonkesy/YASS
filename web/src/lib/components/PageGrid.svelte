<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { Icon } from '$lib/icons';
	import { isIdentity, moveIndex, slotAt } from '$lib/review/reorder';
	import { measure, play, type Positions } from '$lib/review/flip';
	import PageThumb from './PageThumb.svelte';

	let {
		sessionId,
		pages,
		selection,
		reorderable = true,
		scanning = false,
		documents,
		ontoggle,
		onopen,
		onreorder,
		onscanmore
	}: {
		sessionId: string;
		pages: PageDto[];
		selection: ReadonlySet<number>;
		reorderable?: boolean;
		/** Shows a placeholder tile for the page currently coming off the feeder. */
		scanning?: boolean;
		/** Page index to document number, so a split shows where it falls. */
		documents?: Map<number, number>;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
		onreorder: (order: number[]) => void;
		onscanmore?: () => void;
	} = $props();

	// Drag = press, hold ~250 ms without moving, then move. A quick tap stays a tap (selection).
	let grid = $state<HTMLDivElement | null>(null);
	let drag = $state<{ from: number; over: number | null } | null>(null);
	let pressTimer: ReturnType<typeof setTimeout> | null = null;
	let pressStart: { x: number; y: number; index: number } | null = null;
	let rects: DOMRectReadOnly[] = [];

	function slots(): HTMLElement[] {
		return grid ? [...grid.querySelectorAll<HTMLElement>('[data-index]')] : [];
	}

	function pointerdown(e: PointerEvent) {
		if (!reorderable || e.button !== 0) return;
		const target = (e.target as HTMLElement).closest<HTMLElement>('[data-index]');
		if (!target) return;
		const index = Number(target.dataset.index);
		pressStart = { x: e.clientX, y: e.clientY, index };
		pressTimer = setTimeout(() => {
			rects = slots().map((el) => el.getBoundingClientRect());
			drag = { from: index, over: index };
			grid?.setPointerCapture(e.pointerId);
		}, 250);
	}

	function pointermove(e: PointerEvent) {
		if (drag) {
			drag = { ...drag, over: slotAt(rects, e.clientX, e.clientY) };
			e.preventDefault();
			return;
		}
		if (pressStart && Math.hypot(e.clientX - pressStart.x, e.clientY - pressStart.y) > 8)
			cancelPress();
	}

	function pointerup(e: PointerEvent) {
		if (drag) {
			const { from, over } = drag;
			drag = null;
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
</script>

<div
	class="flex max-h-none w-full flex-row gap-3 overflow-x-auto rounded-[22px] bg-surface p-3 shadow-panel lg:sticky lg:top-[72px] lg:max-h-[calc(100dvh-100px)] lg:w-[clamp(140px,11vw,190px)] lg:flex-col lg:overflow-x-visible lg:overflow-y-auto {drag
		? 'cursor-grabbing select-none'
		: ''}"
	role="listbox"
	aria-multiselectable="true"
	aria-label="Pages. Hold and drag to reorder, or use Alt+Arrow keys."
	tabindex="-1"
	bind:this={grid}
	onpointerdown={pointerdown}
	onpointermove={pointermove}
	onpointerup={pointerup}
	onpointercancel={pointerup}
	onkeydown={keydown}
	data-testid="page-grid"
	data-dragging={drag !== null}
>
	{#each pages as page (page.Index)}
		<div
			class="w-[70px] flex-none animate-page-in transition-transform duration-500 ease-spring lg:w-full {drag?.from ===
			page.Index
				? 'opacity-35'
				: ''} {drag && drag.over === page.Index && drag.from !== page.Index
				? 'rounded-[14px] shadow-ring-outline'
				: ''}"
			role="option"
			aria-selected={selection.has(page.Index)}
			data-index={page.Index}
			style="animation-delay: {Math.min(page.Index, 10) * 45}ms"
		>
			<PageThumb
				{sessionId}
				{page}
				selected={selection.has(page.Index)}
				document={page.StartsDocument && (documents?.get(page.Index) ?? 0) > 1
					? documents!.get(page.Index)!
					: null}
				ontoggle={toggle}
				{onopen}
			/>
		</div>
	{/each}

	{#if scanning}
		<div
			class="w-[70px] flex-none rounded-[14px] bg-accent-50 shadow-ring-pending lg:w-full"
			style="aspect-ratio: 1 / 1.2"
			aria-label="Scanning the next page"
		></div>
	{/if}

	{#if onscanmore && !scanning}
		<button
			type="button"
			onclick={onscanmore}
			class="flex w-[70px] flex-none flex-col items-center justify-center gap-1 rounded-[14px] border-[1.5px] border-dashed border-accent-dash text-xs font-bold text-accent transition-colors hover:border-accent hover:bg-accent-hover50 lg:w-full"
			style="aspect-ratio: 1 / 1.2"
		>
			<Icon name="plus" size={16} />
			Scan more
		</button>
	{/if}
</div>
