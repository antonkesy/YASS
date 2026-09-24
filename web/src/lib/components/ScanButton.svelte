<script lang="ts">
	let {
		disabled = false,
		busy = false,
		onclick
	}: { disabled?: boolean; busy?: boolean; onclick: () => void } = $props();
</script>

<button
	type="button"
	class="flex h-40 w-40 flex-col items-center justify-center gap-2 rounded-full bg-teal-700 text-white shadow-lg transition
		hover:bg-teal-600 focus-visible:ring-4 focus-visible:ring-teal-300 focus-visible:outline-none
		active:scale-95 disabled:cursor-not-allowed disabled:bg-stone-400 disabled:shadow-none"
	{disabled}
	aria-busy={busy}
	{onclick}
	data-testid="scan-button"
>
	<svg
		class="h-12 w-12"
		viewBox="0 0 24 24"
		fill="none"
		stroke="currentColor"
		stroke-width="1.8"
		aria-hidden="true"
	>
		<rect x="3" y="7" width="18" height="12" rx="2" />
		<path d="M3 12h18M7 7V5a1 1 0 0 1 1-1h8a1 1 0 0 1 1 1v2" />
	</svg>
	<span class="text-xl font-semibold">{busy ? 'Scanning…' : 'Scan'}</span>
</button>
