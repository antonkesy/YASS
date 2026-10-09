<script lang="ts">
	import { fade } from 'svelte/transition';
	import { ms } from '$lib/motion';

	type Option = { value: string; label: string };

	let {
		name,
		label,
		hint = '',
		options,
		value,
		onchange
	}: {
		/** Also the radio group name, so arrow-key navigation works natively. */
		name: string;
		label: string;
		hint?: string;
		options: Option[];
		value: string;
		onchange: (value: string) => void;
	} = $props();
</script>

<fieldset class="flex flex-col gap-1.5" data-testid="seg-{name}">
	<legend class="mb-1.5 text-[13px] font-bold text-ink-2">
		{label}
		<!-- A new hint fades in over the old one's place rather than swapping in a blink. -->
		{#key hint}{#if hint}<span class="font-semibold text-ink-muted" in:fade={{ duration: ms(220) }}>
					· {hint}</span
				>{/if}{/key}
	</legend>
	<div class="flex rounded-[14px] bg-ctl-track p-1">
		{#each options as option (option.value)}
			<label class="relative flex-1">
				<!--
					The radio covers the segment rather than being sr-only: it stays the real
					control for keyboard and assistive tech, and it is what a pointer hits.
				-->
				<input
					type="radio"
					{name}
					class="peer absolute inset-0 h-full w-full cursor-pointer opacity-0"
					value={option.value}
					checked={value === option.value}
					onchange={() => onchange(option.value)}
				/>
				<span
					class="flex min-h-[36px] items-center justify-center rounded-[10px] px-2 py-1 text-center text-[13px] leading-tight font-bold text-ink-3 transition-all duration-200 peer-checked:bg-surface peer-checked:text-ink peer-checked:shadow-seg peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-accent peer-[:not(:checked):hover]:text-ink"
				>
					{option.label}
				</span>
			</label>
		{/each}
	</div>
</fieldset>
