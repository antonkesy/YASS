<script lang="ts">
	import './layout.css';
	import { onMount } from 'svelte';
	import { getHub } from '$lib/hub/hub';

	let { children } = $props();

	onMount(() => {
		const hub = getHub();
		void hub.start();
	});
</script>

<svelte:head>
	<link rel="icon" href="/favicon.ico" sizes="48x48" />
	<link rel="icon" href="/icons/icon.svg" type="image/svg+xml" />
	<link rel="apple-touch-icon" href="/icons/icon-192.png" />
	<meta name="theme-color" content="#f5f7fc" />
</svelte:head>

<div class="min-h-dvh bg-app text-ink">
	{@render children()}
</div>
