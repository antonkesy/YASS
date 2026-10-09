<script lang="ts">
	import { fly } from 'svelte/transition';
	import { hopKeyframes, ms, play } from '$lib/motion';

	let {
		caption = '',
		pulse = null
	}: {
		/** Shown along the bottom of the front sheet, e.g. "A4 · 300 dpi". */
		caption?: string;
		/** Changes when the scan's settings do — the preset picked — and makes the stack hop. */
		pulse?: string | null;
	} = $props();

	// The stack's outer box drifts on a CSS animation; the hop goes on the box inside it, so
	// the two never fight over the same transform.
	let first = true;
	function hopOnChange(el: Element) {
		void pulse;
		if (first) first = false;
		else play(el, hopKeyframes, 520);
	}
</script>

<!--
	Three sheets, fanned. Offsets are the mockup's; the whole stack drifts up and down and
	a scan head sweeps the front sheet, so the hero has a pulse even when nothing is
	happening. The sweep is decoration, not progress: a real scan leaves this page at once.

	The mockup sized this against width alone (18vw). A sheet is 1.414x taller than it is
	wide, so on a short-but-wide window that made the stack the reason the home screen
	scrolled -- hence the 20vh cap, which binds first on laptop screens.
-->
<div
	class="relative mt-1.5 mb-3.5 w-[150px] flex-none animate-float sm:w-[clamp(118px,min(18vw,17vh),300px)]"
	style="aspect-ratio: 1 / 1.414"
	aria-hidden="true"
>
	<div class="absolute inset-0" {@attach hopOnChange}>
		<div
			class="absolute inset-0 rounded-[10px] bg-accent-stack"
			style="transform: translate(16px, 12px) rotate(4deg)"
		></div>
		<div
			class="absolute inset-0 rounded-[10px] bg-accent-50 shadow-hero-mid"
			style="transform: translate(8px, 6px) rotate(2deg)"
		></div>

		<div class="absolute inset-0 overflow-hidden rounded-[10px] bg-surface shadow-hero">
			{#key caption}
				{#if caption}
					<span
						class="absolute inset-x-0 bottom-[9%] text-center font-mono text-[11px] font-semibold text-ink-faint"
						in:fly={{ y: 8, duration: ms(260), delay: ms(80) }}
						out:fly={{ y: -8, duration: ms(160) }}
					>
						{caption}
					</span>
				{/if}
			{/key}
			<div class="absolute inset-x-0 h-0.5 animate-sweep bg-accent-light shadow-sweep"></div>
		</div>
	</div>
</div>
