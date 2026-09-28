<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { hubError } from '$lib/hub/errors';
	import { toast } from '$lib/toast';
	import { cameFrom } from '$lib/navigation';
	import { getHub, sortedSessions } from '$lib/hub/hub';
	import type { ScanOptionsDto } from '$lib/hub/generated/Yass.Core.Hub';
	import ScanStage from '$lib/components/ScanStage.svelte';
	import PastScansPanel from '$lib/components/PastScansPanel.svelte';
	import { pickEditable, settings } from '$lib/settings';
	import { ExportMode } from '$lib/hub/generated/Yass.Core.Hub';
	import { saveToBrowser } from '$lib/review/download';
	import { exportPrefs } from '$lib/presets';

	const client = getHub();
	const scanner = client.scanner;
	const connection = client.connection;
	const sessions = client.sessions;

	let starting = $state(false);
	let working = $state(false);
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

	// Taken once on mount: coming back from a session slides in from the left and
	// highlights that scan; a cold load just fades.
	const landed = cameFrom.take();
	const cameBack = landed !== null;

	/**
	 * What the next scan will actually use: the server's defaults with this browser's
	 * overrides applied. The stage shows this rather than the bare defaults, which would
	 * advertise settings the scan is not going to run with.
	 */
	const planned = $derived(
		effective ? { ...effective, ...pickEditable($settings, effective.Editable) } : null
	);

	async function scan() {
		starting = true;
		error = null;
		try {
			const options = effective ?? (await client.hub.getEffectiveOptions());
			effective = options;
			const session = await client.hub.startScan(pickEditable($settings, options.Editable));
			await goto(resolve('/session/[id]', { id: session.Id }));
		} catch (e) {
			error = hubError(e);
		} finally {
			starting = false;
		}
	}

	/**
	 * One download per scan. The browser asks before saving several files at once,
	 * which is the right prompt to get: this really is several separate saves.
	 */
	async function downloadAll(ids: string[]) {
		error = null;
		working = true;
		try {
			let files = 0;
			for (const id of ids) {
				files += saveToBrowser(
					await client.hub.export(id, { Mode: ExportMode.Download, Format: $exportPrefs.format })
				);
			}
			toast.show(files === 1 ? 'Downloaded' : `Downloaded ${files} files`);
		} catch (e) {
			error = hubError(e);
		} finally {
			working = false;
		}
	}

	async function deleteAll(ids: string[]) {
		error = null;
		working = true;
		try {
			for (const id of ids) await client.hub.deleteSession(id);
			toast.show(`${ids.length} scan${ids.length === 1 ? '' : 's'} deleted`);
		} catch (e) {
			error = hubError(e);
		} finally {
			working = false;
		}
	}

	async function clearAll() {
		error = null;
		try {
			const gone = await client.hub.clearSessions();
			toast.show(`${gone} scan${gone === 1 ? '' : 's'} deleted`);
		} catch (e) {
			error = hubError(e);
		}
	}

	function openSettings() {
		void goto(resolve('/settings'));
	}
</script>

<svelte:head><title>YASS</title></svelte:head>

<main
	class="mx-auto flex max-w-[2200px] flex-col gap-6 px-4 pt-3 pb-7 sm:px-7 {cameBack
		? 'animate-slide-from-left'
		: 'animate-fade-in'}"
>
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
			options={planned}
			busy={starting}
			onscan={scan}
			onsettings={openSettings}
		/>
		<PastScansPanel
			sessions={recent}
			busy={working}
			onclear={clearAll}
			ondownload={downloadAll}
			ondelete={deleteAll}
			{landed}
		/>
	</div>
</main>
