<script lang="ts">
	import { Icon } from '$lib/icons';

	let {
		disabled = false,
		busy = false,
		onclick
	}: { disabled?: boolean; busy?: boolean; onclick: () => void } = $props();
</script>

<button
	type="button"
	class="flex h-16 w-full max-w-[340px] items-center justify-center gap-3 rounded-[18px] bg-linear-135 from-accent-light to-accent-grad2 text-[20px] font-extrabold tracking-[-0.01em] text-white shadow-scan transition-[transform,box-shadow] duration-200 ease-spring hover:-translate-y-0.5 hover:shadow-scan-hover active:scale-[0.98] disabled:pointer-events-none disabled:opacity-40"
	{disabled}
	aria-busy={busy}
	{onclick}
	data-testid="scan-button"
>
	<Icon name="scan" />
	{busy ? 'Scanning…' : 'Scan'}
</button>
