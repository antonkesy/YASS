<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { Icon } from '$lib/icons';
	import SelectCheckbox from './SelectCheckbox.svelte';

	let {
		sessionId,
		page,
		selected = false,
		current = false,
		selectable = true,
		onshow,
		ontoggle,
		onopen
	}: {
		sessionId: string;
		page: PageDto;
		/** In the toolbar's selection, ticked through the checkbox. */
		selected?: boolean;
		/** The page the stage is showing, picked by tapping the thumbnail. */
		current?: boolean;
		/** Whether the tick box is offered; the filmstrip leaves selecting to the overview. */
		selectable?: boolean;
		onshow: (index: number) => void;
		ontoggle: (index: number) => void;
		/** Opens the page full screen. Left out where the toolbar offers that instead. */
		onopen?: (index: number) => void;
	} = $props();

	// The image on screen may lag behind the page record (edit pending, thumbnail not rendered yet).
	// Track what is actually loaded and CSS-rotate by the difference so edits feel instant.
	let shown = $state<{ hash: string; rotation: number } | null>(null);
	const src = $derived(
		page.ThumbnailReady || shown ? imageUrl(sessionId, page.Index, page.Hash, 'thumb') : null
	);
	const delta = $derived(shown ? page.Rotation - shown.rotation : 0);

	function loaded() {
		shown = { hash: page.Hash, rotation: page.Rotation };
	}
</script>

<div
	class="group relative flex flex-col gap-1 rounded-[14px] p-1.5 transition-[background-color,box-shadow,opacity] duration-250 {current
		? 'bg-accent-50 shadow-ring-selected'
		: selected
			? 'bg-accent-hover50'
			: ''} {page.Deleted ? 'opacity-50' : ''}"
	data-testid="page-thumb"
	data-selected={selected}
	data-current={current}
	data-deleted={page.Deleted}
>
	<button
		type="button"
		class="relative flex w-full items-center justify-center overflow-hidden rounded-[6px] bg-surface shadow-page"
		style="aspect-ratio: 1 / 1.414"
		aria-current={current ? 'true' : undefined}
		aria-label="Show page {page.Index + 1}{page.Deleted ? ', deleted' : ''}"
		onclick={() => onshow(page.Index)}
	>
		{#if src}
			<img
				{src}
				alt=""
				loading="lazy"
				decoding="async"
				draggable="false"
				class="max-h-full max-w-full object-contain transition-transform duration-500 ease-spring"
				style:transform={delta ? `rotate(${delta}deg)` : undefined}
				onload={loaded}
			/>
		{:else}
			<span
				class="h-7 w-7 animate-spin rounded-full border-[3px] border-accent-100 border-t-accent"
				aria-hidden="true"
			></span>
		{/if}

		{#if page.IsBlank}
			<span
				class="pointer-events-none absolute top-1 right-1 rounded-full bg-warning-bg px-1.5 py-0.5 text-[10px] font-extrabold tracking-[0.02em] text-warning-ink"
				data-testid="blank-badge"
			>
				blank
			</span>
		{/if}

		{#if page.Deleted}
			<span
				class="pointer-events-none absolute inset-x-0 bottom-0 bg-danger/90 py-0.5 text-center text-[10px] font-extrabold tracking-[0.02em] text-white"
			>
				deleted
			</span>
		{/if}
	</button>

	{#snippet number()}
		<span
			class="font-mono text-[11px] font-bold {selected || current
				? 'text-accent'
				: 'text-ink-muted'}"
			aria-hidden="true"
		>
			{String(page.Index + 1).padStart(2, '0')}
		</span>
	{/snippet}

	<div class="flex items-center justify-between gap-1 px-0.5">
		{#if selectable}
			<!-- Ticking pages is what the toolbar acts on; tapping the page above only moves the stage. -->
			<button
				type="button"
				class="-m-1 flex items-center gap-1 rounded-full p-1 transition-colors hover:bg-accent-wash"
				aria-pressed={selected}
				aria-label="Select page {page.Index + 1}"
				data-testid="page-select"
				onclick={() => ontoggle(page.Index)}
			>
				<SelectCheckbox checked={selected} size={15} />
				{@render number()}
			</button>
		{:else}
			<span class="px-0.5">{@render number()}</span>
		{/if}
		{#if onopen}
			<button
				type="button"
				class="flex h-6 w-6 items-center justify-center rounded-full text-ink-muted transition-colors hover:bg-accent-wash hover:text-accent"
				aria-label="Open page {page.Index + 1}"
				onclick={(e) => {
					e.stopPropagation();
					onopen(page.Index);
				}}
			>
				<Icon name="expand" size={13} />
			</button>
		{/if}
	</div>
</div>
