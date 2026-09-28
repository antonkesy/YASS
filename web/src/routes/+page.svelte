<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import ScanStage from '$lib/components/ScanStage.svelte';
	import PastScansPanel from '$lib/components/PastScansPanel.svelte';
	import { pickEditable, settings } from '$lib/settings';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;

	let starting = $state(false);
	let error = $state<string | null>(null);
	let effective = $state<ScanOptionsDto | null>(null);

	// The stage shows what the next scan will use, so the options are loaded up front
	// rather than only at the moment Scan is pressed.
	$effect(() => {
		if ($connection === 'connected' && !effective) {
			client.hub
				.getEffectiveOptions()
				.then((o) => (effective = o))
				.catch(() => undefined);
		}
	});

	const recent = $derived(sortedSessions($sessions));

	async function scan() {
		starting = true;
		error = null;
		try {
			const options = effective ?? (await client.hub.getEffectiveOptions());
			effective = options;
			const session = await client.hub.startScan(pickEditable($settings, options.Editable));
			await goto(resolve('/session/[id]', { id: session.Id }));
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		} finally {
			starting = false;
		}
	}

	async function clearAll() {
		error = null;
		try {
			await client.hub.clearSessions();
		} catch (e) {
			error = e instanceof Error ? e.message : String(e);
		}
	}

	function openSettings() {
		void goto(resolve('/settings'));
	}
</script>

<svelte:head><title>YASS</title></svelte:head>

<main class="mx-auto flex max-w-[2200px] flex-col gap-6 px-4 pt-3 pb-7 sm:px-7">
	{#if error}
		<p class="rounded-[14px] bg-danger-bg px-4 py-2 font-semibold text-danger" role="alert">
			{error}
		</p>
	{/if}

	<!-- Header 4rem plus this main's padding, so the two panes fill the window. -->
	<div class="flex min-h-[calc(100dvh-6.5rem)] flex-wrap items-stretch gap-6">
		<ScanStage
			status={$scanner}
			connection={$connection}
			options={effective}
			busy={starting}
			onscan={scan}
			onsettings={openSettings}
		/>
		<PastScansPanel sessions={recent} onclear={clearAll} />
	</div>
</main>
