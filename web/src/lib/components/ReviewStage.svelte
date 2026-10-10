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
		/** Position in `pages` of the page on the stage. */
		current: number;
		/** A scan is running: an empty stage is waiting for its first page, not empty for good. */
		scanning: boolean;
		/** The arrows either side: one page back or on. */
		onstep: (delta: -1 | 1) => void;
	} = $props();

	const shown = $derived(pages[current] ?? null);

	/** The big preview slides in from whichever side the new page came from. */
	let stage = $state<HTMLElement | null>(null);
	/** What the stage image last finished loading: which page, which render, and its turn. */
	let stageLoaded = $state<{ index: number; hash: string; rotation: number } | null>(null);
	// Deliberately the initial value: it tracks which page the stage is showing, and is
	// updated by the effect below rather than following `current` on its own.
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

	/** The page on the stage has an image up — maybe of an older render of it, during an edit. */
	const stageReady = $derived(!!shown && stageLoaded?.index === shown.Index);
	/**
	 * A rotation not yet in the image on screen. The page keeps showing its last render, turned
	 * by CSS, until the new one has loaded: swapping straight to an empty frame with a spinner
	 * in front made every rotate blink.
	 */
	const stageTurn = $derived(stageReady && shown ? shown.Rotation - stageLoaded!.rotation : 0);
</script>

{#if pages.length > 0}
	<!-- Right above the page it counts, and as few characters as say it. -->
	<p
		class="-mb-2 text-[13px] font-semibold text-ink-muted tabular-nums"
		data-testid="page-position"
	>
		{current + 1} / {pages.length}
	</p>
{/if}

{#if shown}
	<!--
		At either end of the scan the arrow that has nowhere to go is not shown, but its
		place is kept (disabled:invisible), so the page stays centred between the two.
	-->
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

		<!--
			The box is reserved at page proportions so the column does not reflow when
			the image arrives; a rotated page letterboxes inside it. Beside the strip the
			page is as tall as the pane allows, and as wide as that height makes it; stacked
			it is as wide as the phone allows.
		-->
		<div
			bind:this={stage}
			class="relative w-full max-w-[max(280px,calc((100dvh-230px)/1.414))] flex-1 lg:h-full lg:w-auto lg:max-w-none lg:flex-none"
			style="aspect-ratio: 1 / 1.414"
			aria-busy={stageLoaded?.hash !== shown.Hash}
		>
			<!--
				A fresh element per page, so the arrows never seem to do nothing; within a
				page the same element, so an edit keeps the last render up until the new
				one replaces it.
			-->
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
				<!-- The preview is rendered on demand, so the empty box sits there for a
				     moment; the same spinner the thumbnails use says it is on the way. -->
				<span
					class="pointer-events-none absolute inset-0 flex items-center justify-center"
					role="status"
					aria-label={t('review.loadingPage', { n: shown.Index + 1 })}
					data-testid="stage-loading"
				>
					<span
						class="h-9 w-9 animate-spin rounded-full border-[3px] border-accent-100 border-t-accent"
						aria-hidden="true"
					></span>
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
			<!-- The same spinner a thumbnail shows while its image is on the way. -->
			<span
				class="h-9 w-9 animate-spin rounded-full border-[3px] border-accent-100 border-t-accent"
				aria-hidden="true"
				data-testid="stage-spinner"
			></span>
			{t('review.waitingFirstPage')}
		{:else}
			{t('review.noPages')}
		{/if}
	</div>
{/if}
