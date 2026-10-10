<script lang="ts">
	import { sortLabels, sortShortLabels, type SortOrder } from '$lib/sessions/browse';
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';
	import Popover from './Popover.svelte';

	let { order, onchange }: { order: SortOrder; onchange: (order: SortOrder) => void } = $props();

	let open = $state(false);
	const orders = Object.keys(sortLabels) as SortOrder[];
</script>

<div class="relative">
	<button
		type="button"
		class="flex h-[38px] items-center gap-1.5 rounded-[12px] px-3 text-[13px] font-bold text-ink-2 transition-colors {open
			? 'bg-ctl-active'
			: 'bg-ctl hover:bg-ctl-active'}"
		aria-expanded={open}
		aria-haspopup="menu"
		onclick={() => (open = !open)}
		data-testid="sort-button"
	>
		<Icon name="sort" />
		{sortShortLabels[order]}
	</button>

	{#if open}
		<Popover
			label={t('home.closeSortMenu')}
			onclose={() => (open = false)}
			class="absolute top-[calc(100%+6px)] right-0 z-31 min-w-[190px] animate-pop-in rounded-[16px] bg-surface p-1.5 shadow-menu"
			testid="sort-menu"
		>
			<p class="px-2.5 py-1.5 text-[12px] font-semibold text-ink-faint">
				{t('home.sortBy')}
			</p>
			{#each orders as value (value)}
				<button
					type="button"
					role="menuitemradio"
					aria-checked={order === value}
					class="flex h-10 w-full items-center gap-2 rounded-[10px] px-2.5 text-left text-sm transition-colors {order ===
					value
						? 'bg-accent-50 font-bold text-accent-deep'
						: 'hover:bg-accent-hover50'}"
					onclick={() => {
						onchange(value);
						open = false;
					}}
				>
					<span class="flex-1">{sortLabels[value]}</span>
					{#if order === value}<span class="text-accent"><Icon name="check" size={14} /></span>{/if}
				</button>
			{/each}
		</Popover>
	{/if}
</div>
