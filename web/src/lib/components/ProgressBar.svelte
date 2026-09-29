<script lang="ts">
	let {
		percent = null,
		label
	}: {
		/** How far along, 0–100. Null is indeterminate: a sliver, so the bar is visibly there. */
		percent?: number | null;
		/** What is progressing, for anyone listening rather than looking. */
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
