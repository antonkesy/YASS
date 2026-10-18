<script lang="ts">
	import type { Snippet } from 'svelte';

	let {
		tone = 'danger',
		tall = false,
		class: place = '',
		children
	}: {
		tone?: 'danger' | 'warning';
		tall?: boolean;
		class?: string;
		children: Snippet;
	} = $props();
</script>

<p
	class="rounded-[14px] px-4 font-semibold select-text {tall ? 'py-3' : 'py-2'} {tone === 'warning'
		? 'bg-warning-bg text-warning-ink'
		: 'bg-danger-bg text-danger'} {place}"
	role="alert"
>
	{@render children()}
</p>
