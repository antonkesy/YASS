<script lang="ts">
	import { filterableStatuses, type StatusFilter } from '$lib/sessions/browse';
	import { statusLabels } from '$lib/format';

	let {
		value,
		counts,
		total,
		onchange
	}: {
		value: StatusFilter;
		counts: Record<string, number>;
		total: number;
		onchange: (value: StatusFilter) => void;
	} = $props();

	const dots: Record<string, string> = {
		draft: 'bg-warning',
		exported: 'bg-success',
		saved: 'bg-saved',
		downloaded: 'bg-downloaded',
		discarded: 'bg-discarded'
	};

	// Only statuses that actually occur get a chip, so the row does not list five
	// filters that would all come back empty.
	const shown = $derived(filterableStatuses.filter((s) => (counts[s] ?? 0) > 0));
</script>

{#if shown.length > 0}
	<div class="flex flex-wrap gap-1.5" data-testid="status-filter">
		{#each [{ key: 'all' as StatusFilter, label: 'All', count: total }, ...shown.map( (s) => ({ key: s as StatusFilter, label: statusLabels[s], count: counts[s] ?? 0 }) )] as chip (chip.key)}
			<button
				type="button"
				class="flex h-[30px] items-center gap-1.5 rounded-full px-2.5 text-xs font-bold transition-colors {value ===
				chip.key
					? 'bg-ink text-white'
					: 'bg-ctl text-ink-2 hover:bg-ctl-hover'}"
				aria-pressed={value === chip.key}
				onclick={() => onchange(chip.key)}
			>
				{#if chip.key !== 'all'}
					<span class="h-[7px] w-[7px] rounded-full {dots[chip.key]}" aria-hidden="true"></span>
				{/if}
				{chip.label}
				<span class="tabular-nums opacity-60">{chip.count}</span>
			</button>
		{/each}
	</div>
{/if}
