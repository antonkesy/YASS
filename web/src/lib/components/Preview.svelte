<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
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
		editable?: boolean;
		/** Keys are left to the open dialog, or Escape would close the preview underneath. */
		paused?: boolean;
		onadjust?: (index: number) => void;
		oncrop?: (index: number) => void;
		onstraighten?: (index: number) => void;
		onfilter?: (index: number, filter: PageFilter) => void;
		oncleanup?: (index: number, cleanup: PageCleanup, on: boolean) => void;
	} = $props();

	let filterOpen = $state(false);
	const tools = $derived([
		{ id: 'adjust', label: t('review.adjust'), icon: 'sliders', run: onadjust },
		{ id: 'crop', label: t('review.crop'), icon: 'crop', run: oncrop },
		{ id: 'straighten', label: t('review.straighten'), icon: 'rotateRight', run: onstraighten }
	] as const);

	const page = $derived(pages[index]);
	let shown = $state<{ hash: string; rotation: number } | null>(null);
	const delta = $derived(shown && page ? page.Rotation - shown.rotation : 0);

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
		'flex h-11 w-11 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70 focus-visible:ring-4 focus-visible:ring-accent focus-visible:outline-none disabled:opacity-30';
</script>

<svelte:window onkeydown={keydown} />

{#if page}
	<div
		class="fixed inset-0 z-30 flex flex-col bg-black text-white select-none"
		role="dialog"
		aria-modal="true"
		aria-label={t('review.pageOf', { n: index + 1, total: pages.length })}
		tabindex="-1"
		bind:this={dialog}
		data-testid="preview"
	>
		<div class="flex items-center gap-2 p-2">
			<button
				type="button"
				class={navBtn}
				aria-label={t('review.closePreview')}
				onclick={onclose}
				data-testid="preview-close"
			>
				<Icon name="close" size={24} stroke={2} />
			</button>
			<span class="text-sm" data-testid="preview-counter"
				>{index + 1} / {pages.length}{page.Deleted ? ` · ${t('review.deleted')}` : ''}</span
			>
			<span class="ml-auto flex gap-1">
				<button
					type="button"
					class={navBtn}
					aria-label={zoomed ? t('review.fitToScreen') : t('review.zoomFullWidth')}
					aria-pressed={zoomed}
					onclick={toggleZoom}
					data-testid="preview-zoom"
				>
					<Icon name={zoomed ? 'zoomOut' : 'zoomIn'} size={20} stroke={2} />
				</button>
				<button
					type="button"
					class={navBtn}
					aria-label={t('review.rotateLeft')}
					onclick={() => onrotate(index, -90)}
				>
					<Icon name="rotateLeft" size={20} stroke={2} />
				</button>
				<button
					type="button"
					class={navBtn}
					aria-label={t('review.rotateRight')}
					onclick={() => onrotate(index, 90)}
				>
					<Icon name="rotateRight" size={20} stroke={2} />
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
						alt={t('review.pageN', { n: index + 1 })}
						class="transition-transform duration-200 {zoomed
							? 'h-auto w-full max-w-none cursor-zoom-out'
							: 'h-full w-full cursor-zoom-in object-contain'} {page.Deleted ? 'opacity-50' : ''}"
						style:transform={delta ? `rotate(${delta}deg)` : undefined}
						draggable="false"
						onload={() => (shown = { hash: page.Hash, rotation: page.Rotation })}
					/>
				</button>
			{/key}
			{#if index > 0}
				<button
					type="button"
					class="{navBtn} fixed top-1/2 left-2 -translate-y-1/2"
					aria-label={t('review.previousPage')}
					onclick={() => onnavigate(index - 1)}
					data-testid="preview-prev"
				>
					<Icon name="chevronLeft" size={24} stroke={2} />
				</button>
			{/if}
			{#if index < pages.length - 1}
				<button
					type="button"
					class="{navBtn} fixed top-1/2 right-2 -translate-y-1/2"
					aria-label={t('review.nextPage')}
					onclick={() => onnavigate(index + 1)}
					data-testid="preview-next"
				>
					<Icon name="chevronRight" size={24} stroke={2} />
				</button>
			{/if}
		</div>

		{#if editable && (onadjust || oncrop || onstraighten || onfilter)}
			<div
				class="flex justify-center px-2 pt-1 pb-[max(0.5rem,env(safe-area-inset-bottom))]"
				data-testid="preview-edit-bar"
			>
				<div
					class="relative flex gap-0.5 rounded-[18px] bg-white/10 p-1"
					role="toolbar"
					aria-label={t('review.editThisPage')}
				>
					{#each tools as tool (tool.id)}
						{#if tool.run}
							{@const run = tool.run}
							<button
								type="button"
								class="flex h-12 w-12 items-center justify-center rounded-[14px] text-white transition-colors hover:bg-white/15"
								onclick={() => run(index)}
								aria-label={tool.label}
								title={tool.label}
								data-testid="preview-{tool.id}"
							>
								<Icon name={tool.icon} />
							</button>
						{/if}
					{/each}
					{#if onfilter}
						<button
							type="button"
							class="flex h-12 w-12 items-center justify-center rounded-[14px] text-white transition-colors {filterOpen
								? 'bg-white/20'
								: 'hover:bg-white/15'}"
							aria-expanded={filterOpen}
							aria-haspopup="menu"
							onclick={() => (filterOpen = !filterOpen)}
							aria-label={t('review.filter')}
							title={t('review.filter')}
							data-testid="preview-filter"
						>
							<Icon name="wand" />
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
