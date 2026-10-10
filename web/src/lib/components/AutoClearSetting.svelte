<script lang="ts">
	import { slide } from 'svelte/transition';
	import { t, type MessageKey } from '$lib/i18n/index.svelte';
	import { ms } from '$lib/motion';
	import { historyPrefs, type RetentionUnit } from '$lib/presets';
	import { retentionUnits } from '$lib/sessions/retention';
	import Toggle from './Toggle.svelte';

	const unitNames: Record<RetentionUnit, MessageKey> = {
		minutes: 'settings.minutes',
		hours: 'settings.hours',
		days: 'settings.days',
		months: 'settings.months'
	};
</script>

<div class="col-span-full flex flex-col rounded-[16px] bg-app px-3.5 py-1.5">
	<Toggle
		name="auto-clear"
		label={t('settings.autoClear')}
		checked={$historyPrefs.autoClear !== null}
		onchange={(on) => historyPrefs.set({ autoClear: on ? { amount: 30, unit: 'days' } : null })}
	/>
	{#if $historyPrefs.autoClear}
		{@const rule = $historyPrefs.autoClear}
		<label
			class="mb-2 flex flex-wrap items-center gap-2 border-t border-line-strong pt-2.5 text-[13px] font-bold text-ink-2"
			transition:slide={{ duration: ms(220) }}
		>
			{t('settings.olderThan')}
			<input
				class="field h-10 w-16 px-2.5 text-center text-sm tabular-nums"
				type="number"
				name="auto-clear-amount"
				min="1"
				step="1"
				value={rule.amount}
				onchange={(e) => {
					// On commit, not per keystroke: clamped as it was typed, clearing the
					// field to type 45 put a 1 back first and left 145.
					const amount = Math.max(1, Math.floor(Number(e.currentTarget.value) || 1));
					e.currentTarget.value = String(amount);
					historyPrefs.set({ autoClear: { ...rule, amount } });
				}}
				data-testid="auto-clear-amount"
			/>
			<select
				class="field h-10 px-2.5 text-sm"
				name="auto-clear-unit"
				value={rule.unit}
				onchange={(e) =>
					historyPrefs.set({
						autoClear: { ...rule, unit: e.currentTarget.value as RetentionUnit }
					})}
				data-testid="auto-clear-unit"
			>
				{#each retentionUnits as unit (unit)}
					<option value={unit}>{t(unitNames[unit])}</option>
				{/each}
			</select>
		</label>
	{/if}
</div>
