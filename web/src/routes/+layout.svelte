<script lang="ts">
	import './layout.css';
	import { onMount } from 'svelte';
	import { onNavigate } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { screenTransition } from '$lib/motion';
	import { escape, typing } from '$lib/keys.svelte';
	import KeyboardHelp from '$lib/components/KeyboardHelp.svelte';
	import { getDesktop, getHub } from '$lib/hub/hub';
	import AppHeader from '$lib/components/AppHeader.svelte';
	import Toast from '$lib/components/Toast.svelte';
	import { i18n } from '$lib/i18n/index.svelte';

	let { children } = $props();

	let shortcuts = $state(false);

	// The app's one Escape: out of a text field first, then the newest open layer ($lib/keys).
	function keydown(e: KeyboardEvent) {
		if (escape(e)) e.preventDefault();
		else if (e.key === '?' && !typing(e)) {
			shortcuts = !shortcuts;
			e.preventDefault();
		}
	}

	// The page starts as `lang="en"` (app.html); this keeps it on the language the UI is in.
	$effect(() => {
		document.documentElement.lang = i18n.locale;
	});

	onMount(() => {
		const hub = getHub();
		void hub.start();
	});

	function click(e: MouseEvent) {
		const desktop = getDesktop();
		const link = (e.target as Element | null)?.closest?.('a[href]');
		if (!desktop || !(link instanceof HTMLAnchorElement) || e.defaultPrevented) return;
		const url = new URL(link.href, location.href);
		if (url.origin === location.origin || !/^https?:$/.test(url.protocol)) return;
		e.preventDefault();
		void desktop.openUrl(url.href);
	}

	// Home is the root: going there is back, leaving it is forward. Settings only cross-fades.
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
	<meta name="theme-color" content="#f5f5f7" media="(prefers-color-scheme: light)" />
	<meta name="theme-color" content="#000000" media="(prefers-color-scheme: dark)" />
</svelte:head>

<!-- *:w-full: a page's mx-auto would otherwise drop the column's stretch and overflow a phone. -->
<div class="flex min-h-dvh flex-col bg-page text-ink *:w-full">
	<AppHeader />
	{@render children()}
</div>

<Toast />

<svelte:window onkeydown={keydown} onclick={click} />

{#if shortcuts}
	<KeyboardHelp onclose={() => (shortcuts = false)} />
{/if}
