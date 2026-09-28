<script lang="ts">
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, pageSizeLabel, sourceLabel } from '$lib/format';

	let {
		options,
		destination = null,
		format = null,
		onclick
	}: {
		options: ScanOptionsDto | null;
		/** Where Export writes, shown first; null when the export button is off. */
		destination?: string | null;
		format?: string | null;
		onclick: () => void;
	} = $props();

	// Only what the scan will actually use; anything the server did not report is left out
	// rather than guessed at.
	const chips = $derived.by(() => {
		if (!options) return [];
		const out: string[] = [];
		if (destination) out.push(`→ ${destination}`);
		const source = sourceLabel(options.Source);
		if (source) out.push(source);
		if (options.Dpi) out.push(`${options.Dpi} dpi`);
		const color = colorLabel(options.Color);
		if (color) out.push(color);
		const size = pageSizeLabel(options.PageSize);
		if (size) out.push(size);
		if (options.AutoDeskew) out.push('Auto-deskew');
		if (options.BlankPageDetection) out.push('Skip blank pages');
		if (options.Brightness || options.Contrast)
			out.push(`B ${options.Brightness ?? 0} / C ${options.Contrast ?? 0}`);
		if (format) out.push(format);
		return out;
	});
</script>

{#if chips.length > 0}
	<div class="flex max-w-[520px] flex-wrap justify-center gap-2" data-testid="settings-chips">
		{#each chips as chip (chip)}
			<button
				type="button"
				{onclick}
				title={chip}
				class="flex h-8 max-w-full items-center truncate rounded-full bg-accent-tint px-3 text-[13px] font-semibold text-accent-deep transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100"
			>
				{chip}
			</button>
		{/each}
	</div>
{/if}
