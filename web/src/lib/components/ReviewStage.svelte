<script lang="ts">
	import { untrack } from 'svelte';
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { t } from '$lib/i18n/index.svelte';
	import { Icon } from '$lib/icons';
	import { play } from '$lib/motion';

	let {
		sessionId,
		pages,
		current,
		scanning,
		onstep
	}: {
		sessionId: string;
		pages: PageDto[];
		current: number;
		scanning: boolean;
		onstep: (delta: -1 | 1) => void;
	} = $props();

	const shown = $derived(pages[current] ?? null);

	let stage = $state<HTMLElement | null>(null);
	let stageLoaded = $state<{ index: number; hash: string; rotation: number } | null>(null);
	// Initial value on purpose: the effect below moves it, not `current` directly.
	let shownAt = untrack(() => current);

	$effect(() => {
		const to = current;
		if (to === shownAt) return;
		const direction = to > shownAt ? 1 : -1;
		shownAt = to;
		if (stage)
			play(
				stage,
				[
					{ opacity: 0, transform: `translateX(${direction * 36}px) scale(0.97)` },
					{ opacity: 1, transform: 'none' }
				],
				340
			);
	});

	const stageReady = $derived(!!shown && stageLoaded?.index === shown.Index);
	// The last render stays up, turned by CSS, until the new one loads; otherwise every rotate blinks.
	const stageTurn = $derived(stageReady && shown ? shown.Rotation - stageLoaded!.rotation : 0);
</script>

{#if pages.length > 0}
	<p
		class="-mb-2 text-[13px] font-semibold text-ink-muted tabular-nums"
		data-testid="page-position"
	>
		{current + 1} / {pages.length}
	</p>
{/if}

{#if shown}
	<!-- disabled:invisible keeps the arrow's place so the page stays centred. -->
	<div class="flex w-full items-center justify-center gap-2 lg:min-h-0 lg:flex-1">
		<button
			type="button"
			class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-surface shadow-nav transition-[background-color,transform] duration-150 hover:scale-105 hover:bg-accent-wash disabled:invisible"
			onclick={() => onstep(-1)}
			disabled={current === 0}
			aria-label={t('review.previousPage')}
			data-testid="stage-prev"
		>
			<Icon name="chevronLeft" />
		</button>

		<div
			bind:this={stage}
			class="relative w-full max-w-[max(280px,calc((100dvh-230px)/1.414))] flex-1 lg:h-full lg:w-auto lg:max-w-none lg:flex-none"
			style="aspect-ratio: 1 / 1.414"
			aria-busy={stageLoaded?.hash !== shown.Hash}
		>
			<!-- Keyed per page; within a page the element stays so an edit keeps the last render up. -->
			{#key shown.Index}
				<img
					src={imageUrl(sessionId, shown.Index, shown.Hash, 'preview')}
					alt={t('review.pageN', { n: shown.Index + 1 })}
					class="h-full w-full rounded-[6px] object-contain drop-shadow-[0_18px_36px_rgb(0_0_0_/_0.14)]"
					style:transform={stageTurn
						? `rotate(${stageTurn}deg) scale(${Math.abs(stageTurn) % 180 ? 0.707 : 1})`
						: undefined}
					onload={() =>
						(stageLoaded = {
							index: shown.Index,
							hash: shown.Hash,
							rotation: shown.Rotation
						})}
				/>
			{/key}
			{#if !stageReady}
				<span
					class="pointer-events-none absolute inset-0 flex items-center justify-center"
					role="status"
					aria-label={t('review.loadingPage', { n: shown.Index + 1 })}
					data-testid="stage-loading"
				>
					<span class="h-9 w-9 spinner" aria-hidden="true"></span>
				</span>
			{/if}
		</div>

		<button
			type="button"
			class="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-surface shadow-nav transition-[background-color,transform] duration-150 hover:scale-105 hover:bg-accent-wash disabled:invisible"
			onclick={() => onstep(1)}
			disabled={current >= pages.length - 1}
			aria-label={t('review.nextPage')}
			data-testid="stage-next"
		>
			<Icon name="chevronRight" />
		</button>
	</div>
{:else}
	<div
		class="flex w-full max-w-[280px] flex-col items-center justify-center gap-3 rounded-[10px] border-[1.5px] border-dashed border-accent-dash text-sm text-ink-muted"
		style="aspect-ratio: 1 / 1.414"
		role={scanning ? 'status' : undefined}
		data-testid="stage-empty"
	>
		{#if scanning}
			<span class="h-9 w-9 spinner" aria-hidden="true" data-testid="stage-spinner"></span>
			{t('review.waitingFirstPage')}
		{:else}
			{t('review.noPages')}
		{/if}
	</div>
{/if}
