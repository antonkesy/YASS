<script lang="ts">
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, pageSizeLabel, sourceLabel } from '$lib/format';

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
	type Chip = { label: string };

	const chipClass =
		'flex h-8 max-w-full items-center gap-1.5 rounded-full bg-accent-tint px-3 text-[13px] font-semibold text-accent-deep';

	/**
	 * The longest label each chip can have, all of them at once: what the area reserves room
	 * for. The scanner and the format do not change with the preset, so they are the real ones.
	 */
	const widest = $derived(
		[
			deviceName,
			'Both sides',
			'1200 dpi',
			'Black & white',
			'Letter',
			'Auto-deskew',
			'Skip blank pages',
			'B -100 / C -100',
			format
		].filter((l): l is string => !!l)
	);

	const chips = $derived.by((): Chip[] => {
		if (!options) return [];
		const plain: string[] = [];
		if (deviceName) plain.push(deviceName);
		const source = sourceLabel(options.Source);
		if (source) plain.push(source);
		if (options.Dpi) plain.push(`${options.Dpi} dpi`);
		const color = colorLabel(options.Color);
		if (color) plain.push(color);
		const size = pageSizeLabel(options.PageSize);
		if (size) plain.push(size);
		if (options.AutoDeskew) plain.push('Auto-deskew');
		if (options.BlankPageDetection) plain.push('Skip blank pages');
		if (options.Brightness || options.Contrast)
			plain.push(`B ${options.Brightness ?? 0} / C ${options.Contrast ?? 0}`);
		if (format) plain.push(format);
		return plain.map((label): Chip => ({ label }));
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
			{#each chips as chip (chip.label)}
				<button
					type="button"
					{onclick}
					title="Change settings"
					class="{chipClass} transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100"
				>
					<span class="truncate">{chip.label}</span>
				</button>
			{/each}
		</div>
	{/if}
</div>
