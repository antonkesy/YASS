<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yawss.Core.Hub';
	import PageThumb from './PageThumb.svelte';

	let {
		sessionId,
		pages,
		selection,
		ontoggle,
		onopen
	}: {
		sessionId: string;
		pages: PageDto[];
		selection: ReadonlySet<number>;
		ontoggle: (index: number) => void;
		onopen: (index: number) => void;
	} = $props();
</script>

<div
	class="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6"
	role="listbox"
	aria-multiselectable="true"
	aria-label="Pages"
	data-testid="page-grid"
>
	{#each pages as page (page.Index)}
		<PageThumb {sessionId} {page} selected={selection.has(page.Index)} {ontoggle} {onopen} />
	{/each}
</div>
