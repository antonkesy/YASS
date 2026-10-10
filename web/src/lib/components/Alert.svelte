<script lang="ts">
	import type { Snippet } from 'svelte';

	let {
		tone = 'danger',
		tall = false,
		class: place = '',
		children
	}: {
		/** Something failed, or something worth knowing went less than well. */
		tone?: 'danger' | 'warning';
		/** Standing in for a whole screen that could not load, rather than above one. */
		tall?: boolean;
		/** Where it sits: a margin, a grid span. */
		class?: string;
		children: Snippet;
	} = $props();
</script>

<p
	class="rounded-[14px] px-4 font-semibold {tall ? 'py-3' : 'py-2'} {tone === 'warning'
		? 'bg-warning-bg text-warning-ink'
		: 'bg-danger-bg text-danger'} {place}"
	role="alert"
>
	{@render children()}
</p>
