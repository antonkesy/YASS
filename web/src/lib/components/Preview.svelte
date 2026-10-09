<script lang="ts">
	import { modified, typing, useLayer } from '$lib/keys.svelte';
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import type { PageCleanup, PageFilter } from '$lib/hub/generated/Yass.Core.Domain';
	import { Icon } from '$lib/icons';
	import FilterMenu from './FilterMenu.svelte';
	import { imageUrl } from '$lib/hub/hub';
	import { detectSwipe, step } from '$lib/review/swipe';

	let {
		sessionId,
		pages,
		index,
		onnavigate,
		onclose,
		onrotate,
		editable = false,
		paused = false,
		onadjust,
		oncrop,
		onstraighten,
		onfilter,
		oncleanup
	}: {
		sessionId: string;
		pages: PageDto[];
		index: number;
		onnavigate: (index: number) => void;
		onclose: () => void;
		onrotate: (index: number, degrees: 90 | -90) => void;
		/** Whether the page can be edited now, which is what brings up the edit bar. */
		editable?: boolean;
		/**
		 * A dialog is open over the preview: keys are left to it, or Escape would close the
		 * preview underneath and the arrows would change the page it is editing.
		 */
		paused?: boolean;
		onadjust?: (index: number) => void;
		oncrop?: (index: number) => void;
		onstraighten?: (index: number) => void;
		onfilter?: (index: number, filter: PageFilter) => void;
		oncleanup?: (index: number, cleanup: PageCleanup, on: boolean) => void;
	} = $props();

	let filterOpen = $state(false);
	const tools = $derived([
		{ id: 'adjust', label: 'Adjust', icon: 'sliders', run: onadjust },
		{ id: 'crop', label: 'Crop', icon: 'crop', run: oncrop },
		{ id: 'straighten', label: 'Straighten', icon: 'rotateRight', run: onstraighten }
	] as const);

	const page = $derived(pages[index]);
	let shown = $state<{ hash: string; rotation: number } | null>(null);
	const delta = $derived(shown && page ? page.Rotation - shown.rotation : 0);

	// Fit-to-screen wastes most of a wide window on a portrait page; zoomed fills the
	// width and scrolls instead, up to the server's processing.preview_max_width.
	// Keyed by page index so moving to another page drops back to fit.
	let zoomedIndex = $state(-1);
	const zoomed = $derived(zoomedIndex === index);
	let viewport = $state<HTMLDivElement | null>(null);

	let start: { x: number; y: number } | null = null;
	let dialog = $state<HTMLDivElement | null>(null);

	$effect(() => {
		dialog?.focus();
	});

	function pointerdown(e: PointerEvent) {
		start = { x: e.clientX, y: e.clientY };
	}

	function pointerup(e: PointerEvent) {
		if (!start) return;
		const direction = detectSwipe(e.clientX - start.x, e.clientY - start.y);
		start = null;
		if (direction) onnavigate(step(index, pages.length, direction));
	}

	const preview = useLayer(() => (zoomed ? (zoomedIndex = -1) : onclose()));

	function keydown(e: KeyboardEvent) {
		if (!page || paused || !preview()?.top || typing(e) || modified(e)) return;
		if (e.key === 'ArrowRight') onnavigate(step(index, pages.length, 'left'));
		else if (e.key === 'ArrowLeft') onnavigate(step(index, pages.length, 'right'));
		else if (e.key === 'z' || e.key === 'Z') toggleZoom();
		else return;
		e.preventDefault();
	}

	function toggleZoom() {
		zoomedIndex = zoomed ? -1 : index;
		if (viewport) viewport.scrollTop = 0;
	}

	const navBtn =
		'flex h-11 w-11 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70 focus-visible:ring-4 focus-visible:ring-teal-300 focus-visible:outline-none disabled:opacity-30';
</script>

<svelte:window onkeydown={keydown} />

{#if page}
	<div
		class="fixed inset-0 z-30 flex flex-col bg-black text-white select-none"
		role="dialog"
		aria-modal="true"
		aria-label="Page {index + 1} of {pages.length}"
		tabindex="-1"
		bind:this={dialog}
		data-testid="preview"
	>
		<div class="flex items-center gap-2 p-2">
			<button
				type="button"
				class={navBtn}
				aria-label="Close preview"
				onclick={onclose}
				data-testid="preview-close"
			>
				<svg
					class="h-6 w-6"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" /></svg
				>
			</button>
			<span class="text-sm" data-testid="preview-counter"
				>{index + 1} / {pages.length}{page.Deleted ? ' · deleted' : ''}</span
			>
			<span class="ml-auto flex gap-1">
				<button
					type="button"
					class={navBtn}
					aria-label={zoomed ? 'Fit page to screen' : 'Zoom to full width'}
					aria-pressed={zoomed}
					onclick={toggleZoom}
					data-testid="preview-zoom"
				>
					<svg
						class="h-5 w-5"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						aria-hidden="true"
						><circle cx="11" cy="11" r="7" /><path d="M20 20l-4.5-4.5" /><path
							d="M8 11h6{zoomed ? '' : 'M11 8v6'}"
						/></svg
					>
				</button>
				<button
					type="button"
					class={navBtn}
					aria-label="Rotate left"
					onclick={() => onrotate(index, -90)}
				>
					<svg
						class="h-5 w-5"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						aria-hidden="true"><path d="M3 12a9 9 0 1 0 3-6.7M3 4v5h5" /></svg
					>
				</button>
				<button
					type="button"
					class={navBtn}
					aria-label="Rotate right"
					onclick={() => onrotate(index, 90)}
				>
					<svg
						class="h-5 w-5"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						aria-hidden="true"><path d="M21 12a9 9 0 1 1-3-6.7M21 4v5h-5" /></svg
					>
				</button>
			</span>
		</div>

		<div
			class="relative flex min-h-0 flex-1 touch-pan-y justify-center {zoomed
				? 'items-start overflow-y-auto overscroll-contain'
				: 'items-center px-2 sm:px-16'}"
			role="presentation"
			bind:this={viewport}
			onpointerdown={pointerdown}
			onpointerup={pointerup}
			onpointercancel={() => (start = null)}
		>
			{#key page.Hash}
				<!-- Tapping the page zooms; the labelled control is the header button, so this is
				     hidden from assistive tech rather than announced twice. -->
				<button
					type="button"
					class="contents"
					tabindex="-1"
					aria-hidden="true"
					onclick={toggleZoom}
				>
					<img
						src={imageUrl(sessionId, page.Index, page.Hash, 'preview')}
						alt="Page {index + 1}"
						class="transition-transform duration-200 {zoomed
							? 'h-auto w-full max-w-none cursor-zoom-out'
							: 'h-full w-full cursor-zoom-in object-contain'} {page.Deleted ? 'opacity-50' : ''}"
						style:transform={delta ? `rotate(${delta}deg)` : undefined}
						draggable="false"
						onload={() => (shown = { hash: page.Hash, rotation: page.Rotation })}
					/>
				</button>
			{/key}
			<button
				type="button"
				class="{navBtn} fixed top-1/2 left-2 -translate-y-1/2"
				aria-label="Previous page"
				disabled={index === 0}
				onclick={() => onnavigate(index - 1)}
				data-testid="preview-prev"
			>
				<svg
					class="h-6 w-6"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg
				>
			</button>
			<button
				type="button"
				class="{navBtn} fixed top-1/2 right-2 -translate-y-1/2"
				aria-label="Next page"
				disabled={index >= pages.length - 1}
				onclick={() => onnavigate(index + 1)}
				data-testid="preview-next"
			>
				<svg
					class="h-6 w-6"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg
				>
			</button>
		</div>

		<!--
			The edit bar, under the page rather than over it, and within reach of a thumb. It acts on
			the page on screen only; selections are the review screen's business.
		-->
		{#if editable && (onadjust || oncrop || onstraighten || onfilter)}
			<div
				class="flex justify-center px-2 pt-1 pb-[max(0.5rem,env(safe-area-inset-bottom))]"
				data-testid="preview-edit-bar"
			>
				<div
					class="relative flex gap-0.5 rounded-[18px] bg-white/10 p-1"
					role="toolbar"
					aria-label="Edit this page"
				>
					{#each tools as tool (tool.id)}
						{#if tool.run}
							{@const run = tool.run}
							<button
								type="button"
								class="flex h-12 min-w-[64px] flex-col items-center justify-center gap-0.5 rounded-[14px] px-2 text-[11px] font-bold text-white transition-colors hover:bg-white/15"
								onclick={() => run(index)}
								data-testid="preview-{tool.id}"
							>
								<Icon name={tool.icon} />
								{tool.label}
							</button>
						{/if}
					{/each}
					{#if onfilter}
						<button
							type="button"
							class="flex h-12 min-w-[64px] flex-col items-center justify-center gap-0.5 rounded-[14px] px-2 text-[11px] font-bold text-white transition-colors {filterOpen
								? 'bg-white/20'
								: 'hover:bg-white/15'}"
							aria-expanded={filterOpen}
							aria-haspopup="menu"
							onclick={() => (filterOpen = !filterOpen)}
							data-testid="preview-filter"
						>
							<Icon name="wand" />
							Filter
						</button>
						{#if filterOpen}
							<FilterMenu
								cleanup={page.Cleanup}
								onfilter={(filter) => onfilter(index, filter)}
								oncleanup={oncleanup ? (cleanup, on) => oncleanup(index, cleanup, on) : undefined}
								onclose={() => (filterOpen = false)}
							/>
						{/if}
					{/if}
				</div>
			</div>
		{/if}
	</div>
{/if}
