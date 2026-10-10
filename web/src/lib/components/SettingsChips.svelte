<script lang="ts">
	import { flip } from 'svelte/animate';
	import { backOut, cubicOut } from 'svelte/easing';
	import { fly, scale } from 'svelte/transition';
	import { ms } from '$lib/motion';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, pageSizeLabel, sourceLabel } from '$lib/format';
	import { t } from '$lib/i18n/index.svelte';

	let {
		options,
		format = null,
		deviceName = null,
		onclick
	}: {
		options: ScanOptionsDto | null;
		format?: string | null;
		deviceName?: string | null;
		onclick: () => void;
	} = $props();

	type Chip = { kind: string; label: string };

	const chipClass =
		'flex h-8 max-w-full items-center gap-1.5 rounded-full bg-ctl px-3 text-[13px] font-semibold text-ink-2';

	let rowHeight = $state(0);

	function trackHeight(row: HTMLElement) {
		if (typeof ResizeObserver === 'undefined') return;
		const observer = new ResizeObserver(() => (rowHeight = row.offsetHeight));
		observer.observe(row);
		return () => observer.disconnect();
	}

	// Keyed by setting, not text, so a changed value slides in place.
	const chips = $derived.by((): Chip[] => {
		if (!options) return [];
		const all: [string, string | null | undefined][] = [
			['device', deviceName],
			['source', sourceLabel(options.Source)],
			['dpi', options.Dpi ? t('home.dpi', { n: options.Dpi }) : null],
			['color', colorLabel(options.Color)],
			['size', pageSizeLabel(options.PageSize)],
			['deskew', options.AutoDeskew ? t('home.autoDeskew') : null],
			['blank', options.BlankPageDetection ? t('home.skipBlankPages') : null],
			[
				'tone',
				options.Brightness || options.Contrast
					? t('home.tone', { b: options.Brightness ?? 0, c: options.Contrast ?? 0 })
					: null
			],
			['format', format]
		];
		return all
			.filter((c): c is [string, string] => !!c[1])
			.map(([kind, label]): Chip => ({ kind, label }));
	});
</script>

<!-- The height follows the chips' rows over a moment, so the Scan button below glides instead of jumping. -->
<div
	class="w-full max-w-[520px] overflow-hidden transition-[height] duration-[240ms] ease-[cubic-bezier(0.2,0.8,0.2,1)]"
	style:height={rowHeight ? `${rowHeight}px` : undefined}
	data-testid="settings-chips-area"
>
	<!-- A little room above and below, so a chip's focus ring is not clipped. -->
	<div
		class="flex flex-wrap justify-center gap-2 py-1"
		{@attach trackHeight}
		data-testid="settings-chips"
	>
		{#each chips as chip (chip.kind)}
			<button
				type="button"
				{onclick}
				title={t('home.changeSettings')}
				class="{chipClass} transition-colors duration-150 hover:bg-ctl-hover hover:text-ink"
				animate:flip={{ duration: ms(260), easing: cubicOut }}
				in:scale={{ start: 0.7, duration: ms(240), easing: backOut }}
			>
				<span class="grid overflow-hidden">
					{#key chip.label}
						<span
							class="col-start-1 row-start-1 truncate"
							in:fly={{ y: 10, duration: ms(220), easing: cubicOut }}
							out:fly={{ y: -10, duration: ms(160) }}>{chip.label}</span
						>
					{/key}
				</span>
			</button>
		{/each}
	</div>
</div>
