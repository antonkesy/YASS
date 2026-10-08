<script lang="ts">
	import './layout.css';
	import { onMount } from 'svelte';
	import { onNavigate } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { screenTransition } from '$lib/motion';
	import { getHub } from '$lib/hub/hub';
	import AppHeader from '$lib/components/AppHeader.svelte';
	import Toast from '$lib/components/Toast.svelte';

	let { children } = $props();

	onMount(() => {
		const hub = getHub();
		void hub.start();
	});

	/**
	 * Screens slide rather than swap, through the browser's View Transitions API; one without
	 * it swaps as it always did. Home is the root, so going there is back and leaving it is
	 * forward. The settings sheet only cross-fades: it brings its own entrance.
	 */
	onNavigate((navigation) => {
		const from = navigation.from?.url.pathname;
		const to = navigation.to?.url.pathname;
		if (!document.startViewTransition || !from || !to || from === to) return;

		const home = resolve('/');
		const settings = resolve('/settings');
		document.documentElement.dataset.nav =
			from === settings || to === settings ? 'fade' : to === home ? 'back' : 'forward';

		screenTransition.start();
		return new Promise((ready) => {
			const transition = document.startViewTransition(async () => {
				ready();
				await navigation.complete;
			});
			void transition.finished.finally(screenTransition.end);
		});
	});
</script>

<svelte:head>
	<link rel="icon" href="/favicon.ico" sizes="48x48" />
	<link rel="icon" href="/icons/icon.svg" type="image/svg+xml" />
	<link rel="apple-touch-icon" href="/icons/icon-192.png" />
	<meta name="theme-color" content="#f5f7fc" />
</svelte:head>

<div class="min-h-dvh bg-app text-ink">
	<AppHeader />
	{@render children()}
</div>

<Toast />
