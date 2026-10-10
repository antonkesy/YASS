<script lang="ts">
	import { t, i18n } from '$lib/i18n/index.svelte';

	let {
		name,
		label,
		value,
		min = -100,
		max = 100,
		step = 5,
		origin = 0,
		onchange
	}: {
		name: string;
		label: string;
		value: number;
		min?: number;
		max?: number;
		step?: number;
		origin?: number;
		onchange: (value: number) => void;
	} = $props();

	const pct = (v: number) => ((v - min) / (max - min)) * 100;
	const from = $derived(Math.min(pct(origin), pct(value)));
	const to = $derived(Math.max(pct(origin), pct(value)));
</script>

<div class="flex flex-col gap-1" data-testid="slider-{name}">
	<div class="flex h-7 items-center justify-between gap-2">
		<label for="slider-{name}" class="text-[13px] font-bold text-ink-2">{label}</label>
		<span class="flex items-center gap-1">
			{#if value !== origin}
				<button
					type="button"
					class="h-7 rounded-full px-2 text-[11px] font-bold text-accent transition-colors hover:bg-accent-wash"
					onclick={() => onchange(origin)}
					aria-label={t('settings.resetLabel', {
						// German capitalises its nouns, so the label stays as it is there.
						label: i18n.locale === 'de' ? label : label.toLowerCase()
					})}
				>
					{t('settings.reset')}
				</button>
			{/if}
			<span
				class="min-w-[44px] rounded-[8px] bg-ctl px-2 py-0.5 text-center text-[12px] font-bold text-ink-2 tabular-nums"
			>
				{value > 0 ? `+${value}` : value}
			</span>
		</span>
	</div>
	<input
		id="slider-{name}"
		class="yass-range h-11 w-full"
		type="range"
		{name}
		{min}
		{max}
		{step}
		{value}
		style="--from: {from}%; --to: {to}%"
		oninput={(e) => onchange(Number(e.currentTarget.value))}
	/>
</div>
