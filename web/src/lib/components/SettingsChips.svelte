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

{#if chips.length > 0}
	<div class="flex max-w-[520px] flex-wrap justify-center gap-2" data-testid="settings-chips">
		{#each chips as chip (chip.label)}
			<button
				type="button"
				{onclick}
				title="Change settings"
				class="flex h-8 max-w-full items-center gap-1.5 rounded-full bg-accent-tint px-3 text-[13px] font-semibold text-accent-deep transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100"
			>
				<span class="truncate">{chip.label}</span>
			</button>
		{/each}
	</div>
{/if}
