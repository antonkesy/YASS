<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
	import ScanButton from '$lib/components/ScanButton.svelte';
	import SessionList from '$lib/components/SessionList.svelte';
	import { pickEditable, settings } from '$lib/settings';
	import { Icon } from '$lib/icons';

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
	<a
		href={resolve('/settings')}
		class="flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-bold text-accent transition-colors hover:bg-accent-wash"
	>
		<Icon name="sliders" />
		Change settings
	</a>

	<ScanButton disabled={!canScan} busy={starting} onclick={scan} />

	{#if error}
		<p class="rounded-lg bg-rose-100 px-4 py-2 text-rose-800" role="alert">{error}</p>
	{/if}

	<section class="w-full">
		<h2 class="mb-2 text-sm font-semibold tracking-wide text-stone-500 uppercase">Recent scans</h2>
		<SessionList sessions={recent} />
	</section>
</main>
