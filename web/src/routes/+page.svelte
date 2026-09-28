<script lang="ts">
	import { goto } from '$app/navigation';
	import { base, resolve } from '$app/paths';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import StatusPill from '$lib/components/StatusPill.svelte';
	import ScanButton from '$lib/components/ScanButton.svelte';
	import SessionList from '$lib/components/SessionList.svelte';
	import { pickEditable, settings } from '$lib/settings';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;

	let starting = $state(false);
	let error = $state<string | null>(null);

	const recent = $derived(sortedSessions($sessions).slice(0, 10));
	const canScan = $derived(
		$connection === 'connected' && $scanner?.State === ScannerState.Ready && !starting
	);

	async function scan() {
		starting = true;
		error = null;
		try {
			const effective = await client.hub.getEffectiveOptions();
			const session = await client.hub.startScan(pickEditable($settings, effective.Editable));
			await goto(resolve('/session/[id]', { id: session.Id }));
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		} finally {
			starting = false;
		}
	}
</script>

<svelte:head><title>YASS</title></svelte:head>

<main class="mx-auto flex max-w-lg flex-col items-center gap-8 px-4 pt-6 pb-12">
	<header class="flex w-full items-center justify-between">
		<h1 class="flex items-center gap-2 text-xl font-bold">
			<img src="{base}/icons/icon.svg" alt="" class="h-8 w-8 rounded-lg" />
			YASS
		</h1>
		<a
			href={resolve('/settings')}
			class="flex min-h-11 min-w-11 items-center justify-center rounded-full text-stone-600 hover:bg-stone-200"
			aria-label="Settings"
		>
			<svg
				class="h-6 w-6"
				viewBox="0 0 24 24"
				fill="none"
				stroke="currentColor"
				stroke-width="1.8"
				aria-hidden="true"
			>
				<circle cx="12" cy="12" r="3" />
				<path
					d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"
				/>
			</svg>
		</a>
	</header>

	<StatusPill status={$scanner} connection={$connection} />

	<ScanButton disabled={!canScan} busy={starting} onclick={scan} />

	{#if error}
		<p class="rounded-lg bg-rose-100 px-4 py-2 text-rose-800" role="alert">{error}</p>
	{/if}

	<section class="w-full">
		<h2 class="mb-2 text-sm font-semibold tracking-wide text-stone-500 uppercase">Recent scans</h2>
		<SessionList sessions={recent} />
	</section>
</main>
