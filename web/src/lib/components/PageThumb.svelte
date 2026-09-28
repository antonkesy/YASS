<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { Icon } from '$lib/icons';

	let {
		sessionId,
		page,
		selected = false,
		document = null,
		ontoggle,
		onopen
	}: {
		sessionId: string;
		page: PageDto;
		selected?: boolean;
		/** Document number to badge this page with, when it opens one past the first. */
		document?: number | null;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
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
	class="group relative flex flex-col gap-1 rounded-[14px] p-1.5 transition-[background-color,box-shadow,opacity] duration-250 {selected
		? 'bg-accent-50 shadow-ring-selected'
		: ''} {page.Deleted ? 'opacity-50' : ''}"
	data-testid="page-thumb"
	data-selected={selected}
	data-deleted={page.Deleted}
>
	{#if document !== null}
		<span
			class="absolute -top-1.5 left-1 z-1 animate-pop-in rounded-full bg-accent px-[7px] py-[3px] text-[10px] font-bold text-white"
			data-testid="doc-badge"
		>
			Doc {document}
		</span>
	{/if}
	<button
		type="button"
		class="relative flex w-full items-center justify-center overflow-hidden rounded-[6px] bg-surface shadow-page"
		style="aspect-ratio: 1 / 1.414"
		aria-pressed={selected}
		aria-label="Page {page.Index + 1}{page.Deleted ? ', deleted' : ''}{selected
			? ', selected'
			: ''}"
		onclick={() => ontoggle(page.Index)}
	>
		{#if src}
			<img
				{src}
				alt=""
				loading="lazy"
				decoding="async"
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

	<div class="flex items-center justify-between gap-1 px-0.5">
		<span
			class="font-mono text-[11px] font-bold {selected ? 'text-accent' : 'text-ink-muted'}"
			aria-hidden="true"
		>
			{String(page.Index + 1).padStart(2, '0')}
		</span>
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
	</div>
</div>
