<script lang="ts">
	let {
		name,
		label,
		hint = '',
		checked,
		onchange
	}: {
		name: string;
		label: string;
		hint?: string;
		checked: boolean;
		onchange: (checked: boolean) => void;
	} = $props();
</script>

<label
	class="relative flex cursor-pointer items-center justify-between gap-4 py-2.5"
	data-testid="toggle-{name}"
>
	<span class="flex flex-col">
		<span class="text-sm font-bold">{label}</span>
		{#if hint}<span class="text-xs text-ink-muted">{hint}</span>{/if}
	</span>
	<input
		type="checkbox"
		{name}
		class="peer absolute inset-0 h-full w-full cursor-pointer opacity-0"
		{checked}
		onchange={(e) => onchange((e.currentTarget as HTMLInputElement).checked)}
	/>
	<!-- The knob is inside the track, so it is moved from the track rather than by peer-checked itself. -->
	<span
		class="relative h-[26px] w-11 flex-none rounded-full bg-toggle-off transition-colors duration-250 peer-checked:bg-accent peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-accent peer-checked:[&>span]:left-[21px]"
	>
		<span
			class="absolute top-[3px] left-[3px] h-5 w-5 rounded-full bg-white shadow-knob transition-[left] duration-250 ease-spring"
		></span>
	</span>
</label>
