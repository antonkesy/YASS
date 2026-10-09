<script lang="ts">
	import { flip } from 'svelte/animate';
	import { backOut, cubicOut } from 'svelte/easing';
	import { fly, scale } from 'svelte/transition';
	import { ms } from '$lib/motion';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, pageSizeLabel, sourceLabel } from '$lib/format';
	import { ColorMode, PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
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
		'flex h-8 max-w-full items-center gap-1.5 rounded-full bg-accent-tint px-3 text-[13px] font-semibold text-accent-deep';

	/**
	 * The longest label each chip can have, all of them at once: what the area reserves room
	 * for. The scanner and the format do not change with the preset, so they are the real ones;
	 * the rest are the longest each setting reads in the current language.
	 */
	const widest = $derived(
		[
			deviceName,
			longest([ScanSource.Feeder, ScanSource.Duplex, ScanSource.Flatbed].map(sourceLabel)),
			t('home.dpi', { n: 1200 }),
			longest([ColorMode.Bw, ColorMode.Grayscale, ColorMode.Color].map(colorLabel)),
			longest([PageSize.A4, PageSize.A5, PageSize.Letter, PageSize.Legal].map(pageSizeLabel)),
			t('home.autoDeskew'),
			t('home.skipBlankPages'),
			t('home.tone', { b: -100, c: -100 }),
			format
		].filter((l): l is string => !!l)
	);

	function longest(labels: string[]): string {
		return labels.reduce((a, b) => (b.length > a.length ? b : a), '');
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
	neither — and the row rewrapped to more or fewer lines, moving the Scan button under it.
	So the area is as tall as the widest set of chips this scan could show, taken from an
	invisible copy stacked in the same grid cell, and the real chips sit centred inside it.
-->
<div class="grid max-w-[520px] place-items-center" data-testid="settings-chips-area">
	<div
		class="invisible col-start-1 row-start-1 flex flex-wrap justify-center gap-2"
		aria-hidden="true"
	>
		{#each widest as label (label)}
			<span class={chipClass}><span class="truncate">{label}</span></span>
		{/each}
	</div>
	{#if chips.length > 0}
		<div
			class="col-start-1 row-start-1 flex flex-wrap justify-center gap-2"
			data-testid="settings-chips"
		>
			{#each chips as chip (chip.kind)}
				<!--
					The others glide to their new places; one that arrives scales in. One that goes
					leaves at once: lingering, it would overflow the room kept for the row.
				-->
				<button
					type="button"
					{onclick}
					title={t('home.changeSettings')}
					class="{chipClass} transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100"
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
	{/if}
</div>
