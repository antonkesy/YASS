<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';

	let {
		sessionId,
		page,
		selected = false,
		ontoggle,
		onopen
	}: {
		sessionId: string;
		page: PageDto;
		selected?: boolean;
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
	class="group relative aspect-[3/4] overflow-hidden rounded-lg border-2 bg-white shadow-sm transition
		{selected ? 'border-teal-600 ring-2 ring-teal-300' : 'border-stone-200'}
		{page.Deleted ? 'opacity-50' : ''}"
	data-testid="page-thumb"
	data-selected={selected}
	data-deleted={page.Deleted}
>
	<button
		type="button"
		class="absolute inset-0 flex h-full w-full items-center justify-center focus-visible:ring-4 focus-visible:ring-teal-300 focus-visible:outline-none"
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
				class="max-h-full max-w-full object-contain transition-transform duration-200"
				style:transform={delta ? `rotate(${delta}deg)` : undefined}
				onload={loaded}
			/>
		{:else}
			<span
				class="h-8 w-8 animate-spin rounded-full border-4 border-stone-200 border-t-teal-600"
				aria-hidden="true"
			></span>
		{/if}
	</button>

	<span
		class="pointer-events-none absolute top-1 left-1 rounded-md bg-black/60 px-1.5 py-0.5 text-xs font-medium text-white"
	>
		{page.Index + 1}
	</span>

	{#if page.IsBlank}
		<span
			class="pointer-events-none absolute top-1 right-1 rounded-md bg-amber-400 px-1.5 py-0.5 text-xs font-semibold text-amber-950"
			data-testid="blank-badge"
		>
			blank
		</span>
	{/if}

	{#if page.Deleted}
		<span
			class="pointer-events-none absolute inset-x-0 bottom-0 bg-rose-600/90 py-0.5 text-center text-xs font-semibold text-white"
		>
			deleted
		</span>
	{/if}

	<button
		type="button"
		class="absolute right-1 bottom-1 flex h-11 w-11 items-center justify-center rounded-full bg-white/90 text-stone-700 shadow
			hover:bg-white focus-visible:ring-4 focus-visible:ring-teal-300 focus-visible:outline-none"
		aria-label="Open page {page.Index + 1}"
		onclick={(e) => {
			e.stopPropagation();
			onopen(page.Index);
		}}
	>
		<svg
			class="h-5 w-5"
			viewBox="0 0 24 24"
			fill="none"
			stroke="currentColor"
			stroke-width="2"
			aria-hidden="true"
		>
			<path d="M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7" />
		</svg>
	</button>
</div>
