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
		/**
		 * The chosen scanner, by name rather than by the id the options carry: a SANE id reads
		 * `epsonds:libusb:001:004` and means nothing on a chip. Null while none is chosen, which
		 * is when the header's own status pill already names whatever the server picked.
		 */
		deviceName?: string | null;
		onclick: () => void;
	} = $props();

	// Only what the scan will actually use; anything the server did not report is left out
	// rather than guessed at.
	type Chip = { kind: string; label: string };

	const chipClass =
		'flex h-8 max-w-full items-center gap-1.5 rounded-full bg-ctl px-3 text-[13px] font-semibold text-ink-2';

	/** How tall the chips' rows are; the area around them follows it over a moment. */
	let rowHeight = $state(0);

	/** Keeps `rowHeight` in step with the chips; where nothing can watch sizes, the area is left to size itself. */
	function trackHeight(row: HTMLElement) {
		if (typeof ResizeObserver === 'undefined') return;
		const observer = new ResizeObserver(() => (rowHeight = row.offsetHeight));
		observer.observe(row);
		return () => observer.disconnect();
	}

	/**
	 * Keyed by what the chip is about, not by its text: Feeder becoming Flatbed is the same
	 * chip with a new value, which slides in place, rather than one chip going and another
	 * arriving.
	 */
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

<!--
	The chips change with the preset — one adds Auto-deskew and a brightness chip, the next has
	neither — so the row can wrap to more or fewer lines. Rather than keep room for the longest
	set (which left a short row floating in the middle of it), the area is exactly as tall as its
	chips, and when that changes it grows or shrinks over a moment: the Scan button under it
	glides instead of jumping.
-->
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
			<!--
				The others glide to their new places; one that arrives scales in. One that goes
				leaves at once, so the row's height follows straight away.
			-->
			<button
				type="button"
				{onclick}
				title={t('home.changeSettings')}
				class="{chipClass} transition-colors duration-150 hover:bg-ctl-hover hover:text-ink"
				animate:flip={{ duration: ms(260), easing: cubicOut }}
				in:scale={{ start: 0.7, duration: ms(240), easing: backOut }}
			>
				<!-- A new value for the same setting slides up into place. -->
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
