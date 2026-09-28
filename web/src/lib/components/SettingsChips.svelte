<script lang="ts">
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import { colorLabel, pageSizeLabel, sourceLabel } from '$lib/format';

	let { options, onclick }: { options: ScanOptionsDto | null; onclick: () => void } = $props();

	// Only what the scan will actually use; anything the server did not report is left out
	// rather than guessed at.
	const chips = $derived.by(() => {
		if (!options) return [];
		const out: string[] = [];
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
		return out;
	});
</script>

{#if chips.length > 0}
	<div class="flex max-w-[520px] flex-wrap justify-center gap-2" data-testid="settings-chips">
		{#each chips as chip (chip)}
			<button
				type="button"
				{onclick}
				class="flex h-8 items-center rounded-full bg-accent-tint px-3 text-[13px] font-semibold text-accent-deep transition-[background-color,transform] duration-150 hover:-translate-y-px hover:bg-accent-100"
			>
				{chip}
			</button>
		{/each}
	</div>
{/if}
