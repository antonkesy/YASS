<script lang="ts">
	let {
		percent = null,
		label
	}: {
		/** 0–100, or null for indeterminate. */
		percent?: number | null;
		label: string;
	} = $props();
</script>

<div
	class="h-1.5 overflow-hidden rounded-full bg-accent-100"
	role="progressbar"
	aria-valuemin="0"
	aria-valuemax="100"
	aria-valuenow={percent ?? undefined}
	aria-label={label}
>
	<div
		class="h-full bg-linear-to-r from-accent-sky to-accent transition-[width] duration-100 ease-linear"
		style:width="{percent ?? 5}%"
	></div>
</div>
