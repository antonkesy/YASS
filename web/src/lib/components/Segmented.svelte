<script lang="ts">
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
		{#if hint}<span class="font-semibold text-ink-muted"> · {hint}</span>{/if}
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
					class="flex h-[34px] items-center justify-center rounded-[10px] px-2 text-center text-[13px] font-bold text-ink-3 transition-all duration-200 peer-checked:bg-surface peer-checked:text-accent peer-checked:shadow-seg peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-accent"
				>
					{option.label}
				</span>
			</label>
		{/each}
	</div>
</fieldset>
