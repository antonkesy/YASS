<script lang="ts">
	import { t } from '$lib/i18n/index.svelte';
	import { PageCleanup, type PageFilter } from '$lib/hub/generated/Yass.Core.Domain';
	import { cleanupOptions, pageFilters } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
	import Popover from './Popover.svelte';

	let {
		cleanup = PageCleanup.None,
		onfilter,
		oncleanup,
		onclose
	}: {
		cleanup?: PageCleanup;
		onfilter: (filter: PageFilter) => void;
		oncleanup?: (cleanup: PageCleanup, on: boolean) => void;
		onclose: () => void;
	} = $props();
</script>

<Popover
	label={t('review.closeFilterMenu')}
	{onclose}
	class="absolute bottom-[calc(100%+8px)] left-1/2 z-31 min-w-[200px] -translate-x-1/2 animate-fade-in rounded-[16px] bg-surface p-1.5 text-ink shadow-menu"
	testid="filter-menu"
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
				class="h-7 w-[22px] flex-none rounded-[5px] shadow-[0_0_0_1px_rgb(0_0_0_/_0.1)]"
				style="background: linear-gradient(135deg, {option.swatch})"
				aria-hidden="true"
			></span>
			{option.label}
		</button>
	{/each}

	{#if oncleanup}
		<div class="mx-1 my-1.5 h-px bg-line" role="separator"></div>
		<span class="block px-2.5 pt-0.5 pb-1 text-[12px] font-semibold text-ink-faint"
			>{t('review.cleanUp')}</span
		>
		{#each cleanupOptions as option (option.value)}
			{@const on = (cleanup & option.value) === option.value}
			<button
				type="button"
				role="menuitemcheckbox"
				aria-checked={on}
				class="flex min-h-[44px] w-full items-center gap-2.5 rounded-[10px] px-2.5 py-1 text-left transition-colors hover:bg-accent-hover50"
				onclick={() => oncleanup(option.value, !on)}
				data-testid="cleanup-{option.value}"
			>
				<span
					class="flex h-5 w-5 flex-none items-center justify-center rounded-[6px] transition-colors {on
						? 'bg-accent text-white'
						: 'shadow-[inset_0_0_0_1.5px] shadow-checkbox'}"
					aria-hidden="true"
				>
					{#if on}<Icon name="checkSmall" />{/if}
				</span>
				<span class="flex flex-col">
					<span class="text-sm font-semibold">{option.label}</span>
					<span class="text-[11px] text-ink-muted">{option.hint}</span>
				</span>
			</button>
		{/each}
	{/if}
</Popover>
