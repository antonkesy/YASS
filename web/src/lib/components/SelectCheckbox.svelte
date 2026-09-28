<script lang="ts">
	import { Icon } from '$lib/icons';

	let { checked, size = 22 }: { checked: boolean; size?: number } = $props();
</script>

<span
	class="flex flex-none animate-check-in items-center justify-center rounded-full shadow-checkbox transition-colors {checked
		? 'bg-accent text-white'
		: 'border-2 border-checkbox bg-white/90'}"
	style="height: {size}px; width: {size}px"
	aria-hidden="true"
>
	{#if checked}<Icon name="check" size={Math.round(size * 0.64)} stroke={2.5} />{/if}
</span>
