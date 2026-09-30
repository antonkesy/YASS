<script lang="ts">
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, folderName, pageSizeLabel, sourceLabel } from '$lib/format';
	import { Icon } from '$lib/icons';

	let {
		options,
		destination = null,
		format = null,
		deviceName = null,
		onclick
	}: {
		options: ScanOptionsDto | null;
		/** Where Export writes, shown first; null when the export button is off. */
		destination?: string | null;
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
	type Chip = { label: string; title?: string; icon?: 'folder' };

	const chips = $derived.by((): Chip[] => {
		if (!options) return [];
		const out: Chip[] = [];
		// The folder's name, not its whole path: a path is as long as the server's disk layout
		// and the chip would have to cut it off before the one part that says anything. The
		// path itself is on the tooltip.
		if (destination)
			out.push({ label: folderName(destination), title: destination, icon: 'folder' });
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
		return [...out, ...plain.map((label): Chip => ({ label }))];
	});
</script>

{#if chips.length > 0}
	<div class="flex max-w-[520px] flex-wrap justify-center gap-2" data-testid="settings-chips">
		{#each chips as chip (chip.label)}
			<button
				type="button"
				{onclick}
				title={chip.title ?? 'Change settings'}
				class="flex h-8 max-w-full items-center gap-1.5 rounded-full bg-accent-tint px-3 text-[13px] font-semibold text-accent-deep transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100"
			>
				{#if chip.icon}<Icon name={chip.icon} size={14} />{/if}
				<span class="truncate">{chip.label}</span>
			</button>
		{/each}
	</div>
{/if}
