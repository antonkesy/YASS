<script lang="ts">
	import { Icon } from '$lib/icons';

	let { checked }: { checked: boolean } = $props();
</script>

<span
	class="flex flex-none animate-check-in items-center justify-center rounded-full shadow-checkbox transition-colors {checked
		? 'bg-accent text-white'
		: 'border-2 border-checkbox bg-white/90'} h-[22px] w-[22px]"
	aria-hidden="true"
>
	{#if checked}<Icon name="check" size={14} stroke={2.5} />{/if}
</span>
