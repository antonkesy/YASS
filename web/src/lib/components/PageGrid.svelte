<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { isIdentity, moveIndex, slotAt } from '$lib/review/reorder';
	import PageThumb from './PageThumb.svelte';

	let {
		sessionId,
		pages,
		selection,
		reorderable = true,
		ontoggle,
		onopen,
		onreorder
	}: {
		sessionId: string;
		pages: PageDto[];
		selection: ReadonlySet<number>;
		reorderable?: boolean;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
		onreorder: (order: number[]) => void;
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
				if (!isIdentity(order)) onreorder(order);
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

	function toggle(index: number) {
		if (!suppressClick) ontoggle(index);
	}

	/** Keyboard reorder: Alt+ArrowLeft/Right moves the focused page. */
	function keydown(e: KeyboardEvent) {
		if (!reorderable || !e.altKey || (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight')) return;
		const target = (e.target as HTMLElement).closest<HTMLElement>('[data-index]');
		if (!target) return;
		const from = Number(target.dataset.index);
		const to = e.key === 'ArrowLeft' ? from - 1 : from + 1;
		if (to < 0 || to >= pages.length) return;
		e.preventDefault();
		onreorder(moveIndex(pages.length, from, to));
		queueMicrotask(() => slots()[to]?.querySelector<HTMLElement>('button')?.focus());
	}
</script>

<div
	class="grid touch-pan-y grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 {drag
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
			class="transition-transform {drag?.from === page.Index ? 'scale-95 opacity-60' : ''} {drag &&
			drag.over === page.Index &&
			drag.from !== page.Index
				? 'rounded-lg ring-4 ring-teal-400'
				: ''}"
			role="option"
			aria-selected={selection.has(page.Index)}
			data-index={page.Index}
		>
			<PageThumb
				{sessionId}
				{page}
				selected={selection.has(page.Index)}
				ontoggle={toggle}
				{onopen}
			/>
		</div>
	{/each}
</div>
