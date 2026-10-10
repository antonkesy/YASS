<script lang="ts">
	import { fly } from 'svelte/transition';
	import { hopKeyframes, ms, play } from '$lib/motion';

	let {
		caption = '',
		pulse = null
	}: {
		caption?: string;
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

<!-- Capped at 20vh: sized by width alone, the stack made short, wide windows scroll. -->
<!--
	The drift has a box of its own: once a script animation (the stage's) has played on an
	element, Chrome stops running its CSS animations on the compositor.
-->
<div
	class="relative mt-1.5 mb-3.5 w-[150px] flex-none sm:w-[clamp(118px,min(18vw,17vh),300px)]"
	style="aspect-ratio: 1 / 1.414"
	aria-hidden="true"
>
	<div class="absolute inset-0 animate-float">
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
				<div class="absolute inset-0 animate-sweep">
					<div class="h-0.5 bg-accent-light shadow-sweep"></div>
				</div>
			</div>
		</div>
	</div>
</div>
