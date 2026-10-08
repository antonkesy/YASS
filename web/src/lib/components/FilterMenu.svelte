<script lang="ts">
	import type { PageFilter } from '$lib/hub/generated/Yass.Core.Domain';
	import { pageFilters } from '$lib/review/edits';

	let {
		placement = 'above',
		onfilter,
		onclose
	}: {
		/** Above the review toolbar at the foot of the page; below the preview's header bar. */
		placement?: 'above' | 'below';
		onfilter: (filter: PageFilter) => void;
		onclose: () => void;
	} = $props();
</script>

<!-- Click-catcher first, so anywhere outside the menu closes it. -->
<button
	type="button"
	class="fixed inset-0 z-30 cursor-default"
	aria-label="Close filter menu"
	onclick={onclose}
></button>
<div
	class="absolute left-1/2 z-31 min-w-[200px] -translate-x-1/2 animate-fade-in rounded-[16px] bg-surface p-1.5 text-ink shadow-menu {placement ===
	'above'
		? 'bottom-[calc(100%+8px)]'
		: 'top-[calc(100%+8px)]'}"
	role="menu"
	data-testid="filter-menu"
>
	{#each pageFilters as option (option.value)}
		<button
			type="button"
			role="menuitem"
			class="flex h-[42px] w-full items-center gap-2.5 rounded-[10px] px-2.5 text-left text-sm font-semibold transition-colors hover:bg-accent-hover50"
			onclick={() => {
				onfilter(option.value);
				onclose();
			}}
		>
			<span
				class="h-7 w-[22px] flex-none rounded-[5px] shadow-[0_0_0_1px_rgb(19_26_43_/_0.1)]"
				style="background: linear-gradient(135deg, {option.swatch})"
				aria-hidden="true"
			></span>
			{option.label}
		</button>
	{/each}
</div>
