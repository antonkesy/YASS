<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import { modified, typing, useLayer } from '$lib/keys.svelte';
	import { tick, untrack, type Snippet } from 'svelte';
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { Icon } from '$lib/icons';
	import { DEFAULT_TILE, fitTile, TILE_SIZES, zoomGrid, zoomTile } from '$lib/review/overview';
	import PageGrid from './PageGrid.svelte';

	let {
		sessionId,
		pages,
		selection,
		current = -1,
		reorderable = true,
		scanning = false,
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
		tile: number | 'fit';
		onzoom: (tile: number) => void;
		onshow: (index: number) => void;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
		onreorder: (order: number[]) => void;
		onselectall: () => void;
		onclose: () => void;
		children?: Snippet;
	} = $props();

	let dialog = $state<HTMLDivElement | null>(null);
	const all = $derived(pages.length > 0 && selection.size === pages.length);

	// Fit is measured, not computed: the grid is drawn hidden at two known sizes and a tile's
	// height is interpolated from them, so it stays right when the markup changes.
	const PROBES = [TILE_SIZES[2], TILE_SIZES[TILE_SIZES.length - 1]] as const;

	let gridEl = $state<HTMLDivElement | null>(null);
	let fitted = $state<number>(DEFAULT_TILE);
	// `tile` is read once on purpose: fitting happens on open; a later switch to 'fit' is for the next one.
	let probe = $state(untrack(() => tile) === 'fit' ? 0 : -1);

	const shown = $derived(tile !== 'fit' ? tile : probe >= 0 ? PROBES[probe] : fitted);

	// Plain, not state: it guards the run below without becoming a reason to run it again.
	let measured = false;

	$effect(() => {
		const grid = gridEl;
		if (measured || !grid || probe < 0) return;
		measured = true;
		void measure(grid);
	});

	async function measure(grid: HTMLDivElement) {
		const samples: { w: number; h: number }[] = [];

		for (let i = 0; i < PROBES.length; i++) {
			probe = i;
			// tick() puts the size on the page; reading offsetWidth then forces the layout.
			await tick();
			const cell = grid.querySelector<HTMLElement>('[data-index]');
			if (!cell) {
				probe = -1;
				return;
			}
			samples.push({ w: cell.offsetWidth, h: cell.offsetHeight });
		}

		const [small, large] = samples;
		const style = getComputedStyle(grid);
		const slope = large.w === small.w ? 0 : (large.h - small.h) / (large.w - small.w);

		fitted = fitTile({
			count: pages.length,
			width: grid.clientWidth - px(style.paddingLeft) - px(style.paddingRight),
			height: grid.clientHeight - px(style.paddingTop) - px(style.paddingBottom),
			gap: px(style.rowGap),
			slope,
			intercept: small.h - slope * small.w
		});
		probe = -1;
	}

	function px(value: string): number {
		return Number.parseFloat(value) || 0;
	}

	$effect(() => {
		dialog?.focus();
	});

	const overview = useLayer(() => onclose());

	function zoom(delta: number) {
		const grid = gridEl;
		const style = grid ? getComputedStyle(grid) : null;
		const width =
			grid && style ? grid.clientWidth - px(style.paddingLeft) - px(style.paddingRight) : 0;
		if (!style || width <= 0) return onzoom(zoomTile(shown, delta));
		onzoom(zoomGrid(shown, delta, width, px(style.columnGap)));
	}

	function keydown(e: KeyboardEvent) {
		if (!overview()?.top || typing(e) || modified(e)) return;
		if (e.key === '+' || e.key === '=') zoom(1);
		else if (e.key === '-' || e.key === '_') zoom(-1);
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
	class="fixed inset-0 z-25 flex flex-col bg-page"
	role="dialog"
	aria-modal="true"
	aria-label={t('review.allPages')}
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
			aria-label={t('review.closeOverview')}
			onclick={onclose}
			data-testid="overview-close"
		>
			<Icon name="close" size={16} />
		</button>
		<p class="text-[15px] font-extrabold tracking-[-0.01em]">{t('review.allPages')}</p>
		<span class="rounded-full bg-app px-2.5 py-1 text-xs font-bold text-ink-muted tabular-nums">
			{t('review.pageCount', { n: pages.length })}{selection.size > 0
				? ` · ${t('review.nSelected', { n: selection.size })}`
				: ''}
		</span>

		<span class="ml-auto flex items-center gap-2">
			<button
				type="button"
				class="{pill} disabled:pointer-events-none disabled:opacity-35"
				disabled={all}
				onclick={onselectall}
				data-testid="overview-select-all"
			>
				<Icon name="check" size={14} />
				{t('review.selectAll')}
			</button>
			<span class="flex items-center gap-1 rounded-full bg-app p-0.5">
				<button
					type="button"
					class="{round} h-8 w-8 bg-transparent"
					aria-label={t('review.zoomOut')}
					disabled={shown <= TILE_SIZES[0]}
					onclick={() => zoom(-1)}
					data-testid="overview-zoom-out"
				>
					<Icon name="zoomOut" />
				</button>
				<button
					type="button"
					class="{round} h-8 w-8 bg-transparent"
					aria-label={t('review.zoomIn')}
					disabled={shown >= TILE_SIZES[TILE_SIZES.length - 1]}
					onclick={() => zoom(1)}
					data-testid="overview-zoom-in"
				>
					<Icon name="zoomIn" />
				</button>
			</span>
		</span>

		<p class="w-full text-xs text-ink-muted">
			{t('review.overviewHint')}
		</p>
	</header>

	<!-- Hidden, not gone, while the two probe sizes are measured: they have to be laid out. -->
	<div class="flex min-h-0 flex-1 flex-col px-3 pt-3 sm:px-6 {probe >= 0 ? 'invisible' : ''}">
		<PageGrid
			layout="overview"
			tile={shown}
			bind:grid={gridEl}
			{sessionId}
			{pages}
			{selection}
			{current}
			{reorderable}
			{scanning}
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
