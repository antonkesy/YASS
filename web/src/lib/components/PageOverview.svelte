<script lang="ts">
	import type { Snippet } from 'svelte';
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { Icon } from '$lib/icons';
	import { TILE_SIZES, zoomTile } from '$lib/review/overview';
	import PageGrid from './PageGrid.svelte';

	let {
		sessionId,
		pages,
		selection,
		current = -1,
		reorderable = true,
		scanning = false,
		documents,
		tile,
		onzoom,
		onshow,
		ontoggle,
		onopen,
		onreorder,
		onselectall,
		onclose,
		children
	}: {
		sessionId: string;
		pages: PageDto[];
		selection: ReadonlySet<number>;
		current?: number;
		reorderable?: boolean;
		scanning?: boolean;
		documents?: Map<number, number>;
		/** Tile width in pixels; the overlay owns the control, the caller owns the value. */
		tile: number;
		onzoom: (tile: number) => void;
		onshow: (index: number) => void;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
		onreorder: (order: number[]) => void;
		onselectall: () => void;
		onclose: () => void;
		/** The page-edit toolbar, so edits act on what was just ticked without leaving. */
		children?: Snippet;
	} = $props();

	let dialog = $state<HTMLDivElement | null>(null);
	const all = $derived(pages.length > 0 && selection.size === pages.length);

	$effect(() => {
		dialog?.focus();
	});

	function keydown(e: KeyboardEvent) {
		if (e.key === 'Escape') onclose();
		else if (e.key === '+' || e.key === '=') onzoom(zoomTile(tile, 1));
		else if (e.key === '-' || e.key === '_') onzoom(zoomTile(tile, -1));
		else return;
		e.preventDefault();
	}

	const round =
		'flex h-9 w-9 flex-none items-center justify-center rounded-full bg-app text-ink transition-colors hover:bg-accent-hover50 disabled:pointer-events-none disabled:opacity-35';
	const pill =
		'flex h-9 items-center gap-1.5 rounded-full bg-app px-3 text-[13px] font-bold transition-colors hover:bg-accent-hover50';
</script>

<!-- Above the app header, below the single-page preview this view can open. -->
<div
	class="fixed inset-0 z-25 flex flex-col bg-app"
	role="dialog"
	aria-modal="true"
	aria-label="All pages"
	tabindex="-1"
	bind:this={dialog}
	onkeydown={keydown}
	data-testid="page-overview"
>
	<header
		class="flex flex-none flex-wrap items-center gap-2 border-b border-line bg-surface px-3 py-2.5"
	>
		<button
			type="button"
			class={round}
			aria-label="Close overview"
			onclick={onclose}
			data-testid="overview-close"
		>
			<Icon name="close" size={16} />
		</button>
		<p class="text-[15px] font-extrabold tracking-[-0.01em]">All pages</p>
		<span class="rounded-full bg-app px-2.5 py-1 text-xs font-bold text-ink-muted tabular-nums">
			{pages.length}
			{pages.length === 1 ? 'page' : 'pages'}{selection.size > 0
				? ` · ${selection.size} selected`
				: ''}
		</span>

		<span class="ml-auto flex items-center gap-2">
			<!-- Clearing is the toolbar's Clear, which is only there once something is ticked. -->
			<button
				type="button"
				class="{pill} disabled:pointer-events-none disabled:opacity-35"
				disabled={all}
				onclick={onselectall}
				data-testid="overview-select-all"
			>
				<Icon name="check" size={14} />
				Select all
			</button>
			<span class="flex items-center gap-1 rounded-full bg-app p-0.5">
				<button
					type="button"
					class="{round} h-8 w-8 bg-transparent"
					aria-label="Zoom out"
					disabled={tile <= TILE_SIZES[0]}
					onclick={() => onzoom(zoomTile(tile, -1))}
					data-testid="overview-zoom-out"
				>
					<Icon name="zoomOut" />
				</button>
				<button
					type="button"
					class="{round} h-8 w-8 bg-transparent"
					aria-label="Zoom in"
					disabled={tile >= TILE_SIZES[TILE_SIZES.length - 1]}
					onclick={() => onzoom(zoomTile(tile, 1))}
					data-testid="overview-zoom-in"
				>
					<Icon name="zoomIn" />
				</button>
			</span>
		</span>

		<p class="w-full text-xs text-ink-muted">
			Drag a page to reorder it · tap to show it · tick to select
		</p>
	</header>

	<div class="flex min-h-0 flex-1 flex-col px-3 pt-3 sm:px-6">
		<PageGrid
			layout="overview"
			{tile}
			{sessionId}
			{pages}
			{selection}
			{current}
			{reorderable}
			{scanning}
			{documents}
			{onshow}
			{ontoggle}
			{onopen}
			{onreorder}
		/>
	</div>

	{#if children}
		<div class="flex flex-none justify-center px-2 pt-1 pb-3 sm:px-3">
			{@render children()}
		</div>
	{/if}
</div>
