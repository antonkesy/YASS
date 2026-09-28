<script lang="ts">
	let {
		caption = '',
		sweeping = false
	}: {
		/** Shown along the bottom of the front sheet, e.g. "A4 · 300 dpi". */
		caption?: string;
		/** Runs the scan head down the page. */
		sweeping?: boolean;
	} = $props();
</script>

<!--
	Three sheets, fanned. Offsets are the mockup's; the whole stack drifts up and down so
	the hero has a pulse even when nothing is happening.

	The mockup sized this against width alone (18vw). A sheet is 1.414x taller than it is
	wide, so on a short-but-wide window that made the stack the reason the home screen
	scrolled -- hence the 20vh cap, which binds first on laptop screens.
-->
<div
	class="relative mt-1.5 mb-3.5 w-[150px] flex-none animate-float sm:w-[clamp(118px,min(18vw,17vh),300px)]"
	style="aspect-ratio: 1 / 1.414"
	aria-hidden="true"
>
	<div
		class="absolute inset-0 rounded-[10px] bg-accent-stack"
		style="transform: translate(16px, 12px) rotate(4deg)"
	></div>
	<div
		class="absolute inset-0 rounded-[10px] bg-accent-50 shadow-hero-mid"
		style="transform: translate(8px, 6px) rotate(2deg)"
	></div>

	<div class="absolute inset-0 overflow-hidden rounded-[10px] bg-surface shadow-hero">
		{#if caption}
			<span
				class="absolute inset-x-0 bottom-[9%] text-center font-mono text-[11px] font-semibold text-ink-faint"
			>
				{caption}
			</span>
		{/if}
		{#if sweeping}
			<div class="absolute inset-x-0 h-0.5 animate-sweep bg-accent-light shadow-sweep"></div>
		{/if}
	</div>
</div>
